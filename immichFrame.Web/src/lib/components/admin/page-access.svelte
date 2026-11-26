<script lang="ts">
	import Btn from './btn.svelte';
	import Toggle from './toggle.svelte';
	import PopupSelect from './popup-select.svelte';
	import Glyph from './glyph.svelte';
	import type { ServerSettings } from '$lib/immichFrameApi';
	import { effective, generalDefaults } from './admin-defaults';
	import { onMount } from 'svelte';
	import type { ServerAccountSettings } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import AccountDetail, { type AccountStatus } from './account-detail.svelte';
	import { accountLabel, libraryFor, monogramOf } from './admin-library.svelte';
	import Monogram from './monogram.svelte';
	import Sheet from './sheet.svelte';
	import ValueRow from './value-row.svelte';
	import FieldRow from './field-row.svelte';
	import InlineConfirm from './inline-confirm.svelte';
	import PageCard from './page-card.svelte';
	import SettingRow from './setting-row.svelte';

	// Pop-up choices, like macOS, with a value set by hand elsewhere kept as one more option.
	const refreshOptions = [
		{ value: '1', label: 'Every Hour' },
		{ value: '6', label: 'Every 6 Hours' },
		{ value: '12', label: 'Every 12 Hours' },
		{ value: '24', label: 'Once a Day' }
	];
	const renewOptions = [
		{ value: '1', label: '1 Day' },
		{ value: '7', label: '1 Week' },
		{ value: '30', label: '30 Days' },
		{ value: '90', label: '90 Days' }
	];
	function withCurrent(
		options: { value: string; label: string }[],
		current: number | null | undefined,
		label: (n: number) => string
	) {
		if (current == null || options.some((o) => o.value === String(current))) return options;
		return [...options, { value: String(current), label: label(current) }];
	}

	interface Props {
		settings: ServerSettings;
		version: number;
		/** Appends a finished account (from the Add sheet). */
		onaddaccount: (account: ServerAccountSettings) => void;
		/** Opens the Add sheet straight away (Photos' Add Account goes through here). */
		startNew?: boolean;
		/** Told once the Add sheet has opened, so a later visit doesn't reopen it. */
		onstartednew?: () => void;
		onremoveaccount: (index: number) => void;
		/** IMMICHFRAME_ADMIN_PASSWORD is set on the server, so the password here has no effect. */
		passwordFromEnvironment?: boolean;
	}

	let {
		settings,
		version,
		onaddaccount,
		startNew = false,
		onstartednew,
		onremoveaccount,
		passwordFromEnvironment = false
	}: Props = $props();

	const general = $derived(settings.General ?? {});
	const get = <K extends keyof typeof generalDefaults>(key: K) =>
		effective(general, generalDefaults, key);

	let secretAction: 'new' | 'off' | null = $state(null);
	function newSecret() {
		const bytes = new Uint8Array(18);
		crypto.getRandomValues(bytes);
		general.authenticationSecret = btoa(String.fromCharCode(...bytes))
			.replace(/\+/g, '-')
			.replace(/\//g, '_');
		secretAction = null;
	}

	let newPassword = $state('');
	let confirmPassword = $state('');
	let passwordReady = $state(false);
	let mismatch = $derived(confirmPassword.length > 0 && newPassword !== confirmPassword);
	let canChange = $derived(newPassword.length >= 8 && newPassword === confirmPassword);
	function setPassword() {
		if (!canChange) return;
		general.adminPassword = newPassword;
		newPassword = confirmPassword = '';
		passwordReady = true;
		passwordSheet = false;
	}
	let passwordSheet = $state(false);

	// Accounts: a list of rows; each opens its details in a sheet.
	let openAccount: number | null = $state(null);
	let statuses: AccountStatus[] = $state([]);
	// Each account's Immich user name labels it (loaded once with its library).
	let libraries = $derived((settings.Accounts ?? []).map((account) => libraryFor(account)));
	const accountName = (account: ServerAccountSettings, i: number) =>
		accountLabel(account, i, libraries[i]);

	async function check(account: ServerAccountSettings): Promise<AccountStatus> {
		try {
			const res = await adminApi.testAccount(JSON.parse(JSON.stringify(account)));
			return res.status === 200
				? {
						testing: false,
						success: !!res.data.success,
						message: res.data.message ?? '',
						version: res.data.version
					}
				: { testing: false, success: false, message: 'The test request failed.' };
		} catch {
			return { testing: false, success: false, message: 'Couldn’t reach the ImmichFrame server.' };
		}
	}

	async function test(i: number) {
		const account = settings.Accounts?.[i];
		if (!account) return;
		statuses[i] = { testing: true };
		statuses[i] = await check(account);
	}

	// A new account lives in this draft until Add: Cancel (or Escape) leaves nothing behind.
	let draft = $state<ServerAccountSettings | null>(null);
	let draftStatus: AccountStatus | undefined = $state();
	let draftReady = $derived(
		!!draft?.immichServerUrl?.trim() && !!(draft?.apiKey?.trim() || draft?.apiKeyFile?.trim())
	);
	function startNewAccount() {
		draft = {};
		draftStatus = undefined;
	}
	function addDraft() {
		if (!draft || !draftReady) return;
		const account = $state.snapshot(draft) as ServerAccountSettings;
		const status = draftStatus;
		draft = null;
		onaddaccount(account);
		if (status) statuses[(settings.Accounts?.length ?? 1) - 1] = status;
	}

	onMount(() => {
		(settings.Accounts ?? []).forEach((account, i) => {
			if (account.immichServerUrl && (account.apiKey || account.apiKeyFile)) test(i);
		});
	});

	onMount(() => {
		if (startNew) {
			startNewAccount();
			onstartednew?.();
		}
	});

	let advancedOpen = $state(false);
	// Search can jump to a setting inside Advanced: open it first.
	$effect(() => {
		const reveal = (e: Event) => {
			const title = (e as CustomEvent<string>).detail;
			if (['Keep images on disk', 'Refresh albums and people', 'Webhook'].includes(title)) {
				advancedOpen = true;
			}
		};
		window.addEventListener('if-reveal', reveal);
		return () => window.removeEventListener('if-reveal', reveal);
	});
</script>

<div class="flex flex-col gap-7">
	<PageCard
		title="Immich accounts"
		footer="The frame only reads from Immich. Each account's key stays on the server."
	>
		{#each settings.Accounts ?? [] as account, index (index)}
			{@const status = statuses[index]}
			<button
				id={`account-row-${index}`}
				type="button"
				class="border-sep flex min-h-14 w-full items-center gap-3 border-t py-2 text-left first:border-t-0 focus-visible:outline-2 focus-visible:outline-primary"
				onclick={() => (openAccount = index)}
			>
				<Monogram name={monogramOf(accountName(account, index))} avatarOf={index} />
				<span class="min-w-0 flex-1">
					<span class="block text-[0.9375rem] md:text-[0.875rem]"
						>{accountName(account, index)}</span
					>
					<span
						class="text-label-2 flex items-center gap-1.5 truncate text-[0.8125rem] md:text-[0.75rem]"
					>
						{#if status?.testing}
							Checking…
						{:else if status?.success}
							<span class="h-1.5 w-1.5 shrink-0 rounded-full bg-[#30d158]"></span>
							Connected&nbsp;·&ensp;Immich {status.version}
						{:else if status}
							<span class="h-1.5 w-1.5 shrink-0 rounded-full bg-[#ff3b30]"></span> Not connected
						{:else}
							{account.immichServerUrl || 'Not set up'}
						{/if}
					</span>
				</span>
				<span class="text-label-3"><Glyph name="chevronRight" size={16} stroke={2} /></span>
			</button>
		{/each}
		<button
			id="add-account"
			type="button"
			class="border-sep flex min-h-11 w-full items-center border-t py-2 text-left text-[0.9375rem] text-primary first:border-t-0 md:text-[0.875rem]"
			onclick={startNewAccount}
		>
			Add Immich Account…
		</button>
	</PageCard>

	{#if openAccount !== null && settings.Accounts?.[openAccount]}
		{#key `${version}-${openAccount}`}
			<Sheet
				title={accountName(settings.Accounts[openAccount], openAccount)}
				returnFocusTo={`account-row-${openAccount}`}
				hideTitle
				onclose={() => (openAccount = null)}
			>
				<AccountDetail
					account={settings.Accounts[openAccount]}
					name={accountName(settings.Accounts[openAccount], openAccount)}
					index={openAccount}
					immichName={libraries[openAccount]?.userName}
					status={statuses[openAccount]}
					ontest={() => openAccount !== null && test(openAccount)}
					onremove={() => {
						const i = openAccount!;
						openAccount = null;
						statuses = statuses.filter((_, j) => j !== i);
						onremoveaccount(i);
					}}
				/>
			</Sheet>
		{/key}
	{/if}

	{#if draft}
		<Sheet
			title="New Account"
			returnFocusTo="add-account"
			onclose={() => (draft = null)}
			confirm={{ label: 'Add', disabled: !draftReady, onconfirm: addDraft }}
		>
			<AccountDetail
				account={draft}
				name="New Account"
				status={draftStatus}
				ontest={async () => {
					if (!draft) return;
					draftStatus = { testing: true };
					draftStatus = await check(draft);
				}}
			/>
		</Sheet>
	{/if}

	<PageCard
		title="Frame secret"
		footer={general.authenticationSecret
			? 'Screens send this to show photos.'
			: 'There’s no secret, so anyone who can reach the frame can see the photos.'}
	>
		{#if general.authenticationSecret}
			<ValueRow title="Secret" value={general.authenticationSecret} secret />
		{/if}
		{#if secretAction}
			<div class="pb-3">
				<InlineConfirm
					message={secretAction === 'new'
						? 'After you save, every screen needs the new secret before it shows photos again.'
						: 'After you save, anyone who can reach the frame can see the photos.'}
					confirmLabel={secretAction === 'new' ? 'Create Secret' : 'Turn Off'}
					danger={secretAction === 'off'}
					returnFocusTo="secret-new"
					onconfirm={() => {
						if (secretAction === 'new') newSecret();
						else {
							general.authenticationSecret = null;
							secretAction = null;
						}
					}}
					oncancel={() => (secretAction = null)}
				/>
			</div>
		{:else}
			<button
				id="secret-new"
				type="button"
				class="border-sep flex min-h-11 w-full items-center border-t py-2 text-left text-[0.9375rem] text-primary first:border-t-0 md:text-[0.875rem]"
				onclick={() => (secretAction = 'new')}
			>
				{general.authenticationSecret ? 'Create New Secret…' : 'Create Secret…'}
			</button>
			{#if general.authenticationSecret}
				<button
					type="button"
					class="border-sep flex min-h-11 w-full items-center border-t py-2 text-left text-[0.9375rem] text-[#d70015] md:text-[0.875rem] dark:text-[#ff6961]"
					onclick={() => (secretAction = 'off')}
				>
					Turn Off Secret…
				</button>
			{/if}
		{/if}
	</PageCard>

	<PageCard
		title="Admin password"
		footer={passwordFromEnvironment
			? 'Set by IMMICHFRAME_ADMIN_PASSWORD on the server. Change it there and restart ImmichFrame.'
			: passwordReady
				? 'The new password takes effect when you save.'
				: 'For this page only. Screens never need it.'}
	>
		{#if passwordFromEnvironment}
			<div class="flex min-h-11 items-center justify-between py-2">
				<span class="text-[0.9375rem] md:text-[0.875rem]">Password</span>
				<span class="text-label-2 text-[0.9375rem] md:text-[0.875rem]">Set on the server</span>
			</div>
		{:else}
			<button
				id="change-password"
				type="button"
				class="flex min-h-11 w-full items-center py-2 text-left text-[0.9375rem] text-primary md:text-[0.875rem]"
				onclick={() => (passwordSheet = true)}
			>
				Change Password…
			</button>
		{/if}
	</PageCard>

	{#if passwordSheet}
		<Sheet
			title="Change Password"
			returnFocusTo="change-password"
			onclose={() => (passwordSheet = false)}
		>
			{#snippet actions()}
				<Btn
					size="small"
					variant="outline"
					color="secondary"
					onclick={() => (passwordSheet = false)}>Cancel</Btn
				>
				<Btn size="small" type="submit" form="password-form" disabled={!canChange}
					>Change Password</Btn
				>
			{/snippet}
			<form
				id="password-form"
				onsubmit={(e) => {
					e.preventDefault();
					setPassword();
				}}
			>
				<PageCard
					footer={mismatch
						? 'The passwords don’t match.'
						: 'At least 8 characters. Screens never need it.'}
				>
					<FieldRow
						title="New password"
						type="password"
						autocomplete="new-password"
						placeholder="Required"
						value={newPassword}
						oninput={(v) => (newPassword = v)}
					/>
					<FieldRow
						title="Confirm"
						type="password"
						autocomplete="new-password"
						placeholder="Required"
						value={confirmPassword}
						oninput={(v) => (confirmPassword = v)}
					/>
				</PageCard>
				<!-- Phone: the sheet has no bottom bar, so the button sits under the fields. -->
				<div class="mt-5 flex justify-end md:hidden">
					<Btn type="submit" disabled={!canChange}>Change Password</Btn>
				</div>
			</form>
		</Sheet>
	{/if}

	<PageCard title="Advanced">
		<button
			type="button"
			class="flex min-h-11 w-full items-center justify-between gap-3 py-2 text-left focus-visible:outline-2 focus-visible:outline-primary"
			aria-expanded={advancedOpen}
			onclick={() => (advancedOpen = !advancedOpen)}
		>
			<span>
				<span class="block text-[0.9375rem] md:text-[0.875rem]">Cache, refresh and webhook</span>
			</span>
			<span class="text-label-3 transition-transform duration-200 {advancedOpen ? 'rotate-90' : ''}"
				><Glyph name="chevronRight" size={16} stroke={2} /></span
			>
		</button>
		{#if advancedOpen}
			<div class="border-sep border-t">
				<SettingRow
					title="Keep images on disk"
					description="Downloads each photo once and serves it from a local cache"
				>
					{#snippet control()}
						<Toggle
							aria-label="Keep images on disk"
							checked={get('downloadImages')}
							onCheckedChange={(v) => (general.downloadImages = v)}
						/>
					{/snippet}
					{#if get('downloadImages')}
						<SettingRow title="Download again after" nested>
							{#snippet control()}
								<PopupSelect
									label="Download again after"
									options={withCurrent(
										renewOptions,
										get('renewImagesDuration'),
										(n) => `${n} days`
									)}
									value={String(get('renewImagesDuration'))}
									onchange={(v) => (general.renewImagesDuration = Number(v))}
								/>
							{/snippet}
						</SettingRow>
					{/if}
				</SettingRow>
				<SettingRow
					title="Refresh albums and people"
					description="How often the frame re-reads what's in them"
				>
					{#snippet control()}
						<PopupSelect
							label="Refresh albums and people"
							options={withCurrent(refreshOptions, get('refreshAlbumPeopleInterval'), (n) =>
								n === 0 ? 'Every Request' : `Every ${n} Hours`
							)}
							value={String(get('refreshAlbumPeopleInterval'))}
							onchange={(v) => (general.refreshAlbumPeopleInterval = Number(v))}
						/>
					{/snippet}
				</SettingRow>
				<FieldRow
					title="Webhook"
					type="url"
					placeholder="https://…"
					value={general.webhook ?? ''}
					oninput={(v) => (general.webhook = v)}
				/>
				<p class="text-label-2 pb-2 text-[0.75rem]">
					Called each time a screen shows a photo, for home automation.
				</p>
			</div>
		{/if}
	</PageCard>
</div>
