using ImmichFrame.Core.Api;
using ImmichFrame.Core.Exceptions;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.WebApi.Helpers;
using ImmichFrame.WebApi.Models;
using ImmichFrame.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImmichFrame.WebApi.Controllers
{
    public class AdminStatusDto
    {
        /// <summary>What the admin UI should show.</summary>
        public AdminUiState State { get; set; } = AdminUiState.Disabled;
    }

    public class AdminSetupDto
    {
        public string AdminPassword { get; set; } = string.Empty;
    }

    public class SettingsUpdateResultDto
    {
        public List<string> Warnings { get; set; } = new();
    }

    public class AccountTestResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Version { get; set; }
    }

    public class AdminInfoDto
    {
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// The admin password comes from IMMICHFRAME_ADMIN_PASSWORD, so changing it in the
        /// admin UI has no effect.
        /// </summary>
        public bool PasswordFromEnvironment { get; set; }
    }

    public class ScreenRenameDto
    {
        public string? Name { get; set; }
    }

    public class AdminPreviewAssetDto
    {
        public Guid Id { get; set; }
        public string? FileName { get; set; }
        public DateTime? TakenAt { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public List<string> People { get; set; } = new();
        public string Type { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = ImmichFrameAdminAuthenticationHandler.SchemeName)]
    public class AdminController : ControllerBase
    {
        private readonly ILogger<AdminController> _logger;
        private readonly SettingsService _settingsService;
        private readonly AdminAuthService _adminAuthService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ScreenRegistry _screenRegistry;
        private readonly IImmichFrameLogic _logic;

        /// <summary>Most assets the "Up next" preview returns.</summary>
        public const int MaxPreviewCount = 12;

        public AdminController(ILogger<AdminController> logger, SettingsService settingsService,
            AdminAuthService adminAuthService, IHttpClientFactory httpClientFactory,
            ScreenRegistry screenRegistry, IImmichFrameLogic logic)
        {
            _logger = logger;
            _settingsService = settingsService;
            _adminAuthService = adminAuthService;
            _httpClientFactory = httpClientFactory;
            _screenRegistry = screenRegistry;
            _logic = logic;
        }

        [HttpGet("Status", Name = "GetAdminStatus")]
        [AllowAnonymous]
        [AllowWhenFileManaged]
        public AdminStatusDto GetStatus()
        {
            return new AdminStatusDto { State = _adminAuthService.State };
        }

        /// <summary>
        /// Claims an unconfigured instance by setting the admin password. Anonymous on
        /// purpose: without it a fresh install has no way into the admin UI. Closes for
        /// good as soon as a password exists, from either the environment or the database.
        /// Any settings imported from a config file are preserved.
        /// </summary>
        [HttpPost("Setup", Name = "SetupAdmin")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(SettingsUpdateResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<SettingsUpdateResultDto>> Setup([FromBody] AdminSetupDto setup)
        {
            if (string.IsNullOrWhiteSpace(setup.AdminPassword))
            {
                return Problem(detail: "An admin password is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            SetupResult result;
            try
            {
                result = await _settingsService.TryClaimSetupAsync(
                    setup.AdminPassword, () => _adminAuthService.SetupRequired);
            }
            catch (SettingsNotValidException ex)
            {
                _logger.LogWarning("Rejected setup: {message}", ex.Message);
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }

            if (result == SetupResult.AlreadyClaimed)
            {
                return Problem(detail: "ImmichFrame is already configured.", statusCode: StatusCodes.Status409Conflict);
            }

            _logger.LogInformation("Admin password set through onboarding.");
            return Ok(new SettingsUpdateResultDto());
        }

        /// <summary>
        /// The raw settings for editing. Secrets are included on purpose: the caller is
        /// authenticated with the dedicated admin password, and masking would break the
        /// GET-edit-PUT round-trip.
        /// </summary>
        [HttpGet("Settings", Name = "GetAdminSettings")]
        public ServerSettings GetSettings()
        {
            // Secrets in the body: keep them out of browser and proxy caches.
            Response.Headers.CacheControl = "no-store";
            return _settingsService.GetRawSettings();
        }

        [HttpPut("Settings", Name = "UpdateAdminSettings")]
        [ProducesResponseType(typeof(SettingsUpdateResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SettingsUpdateResultDto>> UpdateSettings([FromBody] ServerSettings settings)
        {
            Core.Interfaces.IServerSettings validated;
            try
            {
                validated = await _settingsService.UpdateAsync(settings);
            }
            catch (SettingsNotValidException ex)
            {
                _logger.LogWarning("Rejected settings update: {message}", ex.Message);
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }

            // Non-blocking checks: saving is allowed even if a server is currently unreachable
            // (e.g. pre-provisioning), but the admin should know about it.
            var checks = await Task.WhenAll(validated.Accounts
                .Select(account => ImmichServerVersionChecker.CheckAccount(account, _httpClientFactory)));

            return Ok(new SettingsUpdateResultDto
            {
                Warnings = checks.Where(c => !c.Success).Select(c => c.Message).ToList()
            });
        }

        [HttpPost("Settings/TestAccount", Name = "TestAccount")]
        public async Task<AccountTestResultDto> TestAccount([FromBody] ServerAccountSettings account)
        {
            try
            {
                account.ValidateAndInitialize(); // resolves ApiKeyFile if given
            }
            catch (Exception ex)
            {
                return new AccountTestResultDto { Success = false, Message = ex.Message };
            }

            var check = await ImmichServerVersionChecker.CheckAccount(account, _httpClientFactory);
            return new AccountTestResultDto { Success = check.Success, Message = check.Message, Version = check.Version };
        }
            [HttpGet("Info", Name = "GetAdminInfo")]
        public AdminInfoDto GetInfo()
        {
            return new AdminInfoDto
            {
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                PasswordFromEnvironment = _adminAuthService.PasswordFromEnvironment
            };
        }

        /// <summary>
        /// What an account can pick from: albums, named people and tags, plus photo counts. Takes
        /// the account settings in the body, so it works for an account that isn't saved yet.
        /// </summary>
        [HttpPost("Library", Name = "GetAccountLibrary")]
        public async Task<AccountLibraryDto> GetAccountLibrary([FromBody] ServerAccountSettings account)
        {
            try
            {
                account.ValidateAndInitialize(); // resolves ApiKeyFile if given
            }
            catch (Exception ex)
            {
                return new AccountLibraryDto { Success = false, Message = ex.Message };
            }

            return await ImmichAccountLibrary.Load(account, _httpClientFactory);
        }

        [HttpGet("Screens", Name = "GetScreens")]
        public async Task<IReadOnlyList<ScreenDto>> GetScreens()
        {
            return await _screenRegistry.GetScreensAsync();
        }

        [HttpPut("Screens/{clientIdentifier}/Name", Name = "RenameScreen")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RenameScreen(string clientIdentifier, [FromBody] ScreenRenameDto rename)
        {
            return await _screenRegistry.RenameAsync(clientIdentifier, rename.Name) ? NoContent() : NotFound();
        }

        [HttpDelete("Screens/{clientIdentifier}", Name = "ForgetScreen")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ForgetScreen(string clientIdentifier)
        {
            return await _screenRegistry.ForgetAsync(clientIdentifier) ? NoContent() : NotFound();
        }

        /// <summary>Screens turned away in the past week for a wrong or missing frame secret, newest first.</summary>
        [HttpGet("Screens/Rejected", Name = "GetRejectedScreens")]
        public async Task<IReadOnlyList<RejectedScreenDto>> GetRejectedScreens()
        {
            return await _screenRegistry.GetRejectedAsync();
        }

        [HttpDelete("Screens/Rejected/{clientIdentifier}", Name = "DismissRejectedScreen")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult DismissRejectedScreen(string clientIdentifier)
        {
            return _screenRegistry.DismissRejected(clientIdentifier) ? NoContent() : NotFound();
        }

        /// <summary>The current settings version and the kept previous ones, newest first.</summary>
        [HttpGet("Settings/History", Name = "GetSettingsHistory")]
        public async Task<List<SettingsHistoryEntryDto>> GetSettingsHistory()
        {
            return await _settingsService.GetHistoryAsync();
        }

        /// <summary>
        /// Makes a previous version current again. This is a new save, so it can be undone in turn.
        /// The admin password is kept as it is now.
        /// </summary>
        [HttpPost("Settings/History/{version:long}/Restore", Name = "RestoreSettingsVersion")]
        [ProducesResponseType(typeof(SettingsUpdateResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SettingsUpdateResultDto>> RestoreSettingsVersion(long version)
        {
            Core.Interfaces.IServerSettings? restored;
            try
            {
                restored = await _settingsService.RestoreAsync(version);
            }
            catch (SettingsNotValidException ex)
            {
                _logger.LogWarning("Rejected restoring settings version {version}: {message}", version, ex.Message);
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }

            if (restored == null)
            {
                return Problem(detail: $"Settings version {version} is not in the history.", statusCode: StatusCodes.Status404NotFound);
            }

            var checks = await Task.WhenAll(restored.Accounts
                .Select(account => ImmichServerVersionChecker.CheckAccount(account, _httpClientFactory)));
            return Ok(new SettingsUpdateResultDto
            {
                Warnings = checks.Where(c => !c.Success).Select(c => c.Message).ToList()
            });
        }

        /// <summary>
        /// A sample of what the frame shows next, drawn from the same pool as the slideshow (one
        /// pool request, like a screen asking for its next batch).
        /// </summary>
        [HttpGet("Preview", Name = "GetAdminPreview")]
        public async Task<List<AdminPreviewAssetDto>> GetAdminPreview(int count = 4)
        {
            count = ClampPreviewCount(count);
            var assets = (await _logic.GetAssets()).ToList();
            return assets
                .DistinctBy(a => a.Id)
                .Take(count)
                .Select(a => new AdminPreviewAssetDto
                {
                    Id = a.Id,
                    FileName = a.OriginalFileName,
                    TakenAt = a.LocalDateTime == default ? null : a.LocalDateTime.DateTime,
                    City = a.ExifInfo?.City,
                    Country = a.ExifInfo?.Country,
                    People = (a.People ?? new List<PersonResponseDto>())
                        .Select(p => p.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList(),
                    Type = a.Type.ToString()
                })
                .ToList();
        }

        internal static int ClampPreviewCount(int count) => Math.Clamp(count, 1, MaxPreviewCount);

        /// <summary>The image of a preview asset. Fetched with the admin password, so the UI loads it as a blob.</summary>
        [HttpGet("Preview/{id}/Image", Name = "GetAdminPreviewImage")]
        [Produces("image/jpeg", "image/webp")]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAdminPreviewImage(Guid id)
        {
            var asset = await _logic.GetAsset(id, AssetTypeEnum.IMAGE);
            if (asset.Owner != null)
            {
                HttpContext.Response.RegisterForDispose(asset.Owner);
            }

            Response.Headers.CacheControl = "private, max-age=300";
            return File(asset.FileStream, asset.ContentType, asset.FileName);
        }
    }
}
