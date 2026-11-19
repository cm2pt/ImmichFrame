<script lang="ts">
	import Btn from './btn.svelte';
	import Toggle from './toggle.svelte';
	import { Input, NumberInput } from '@immich/ui';
	import Glyph from './glyph.svelte';
	import type { ServerSettings } from '$lib/immichFrameApi';
	import { effective, generalDefaults } from './admin-defaults';
	import { onMount } from 'svelte';
	import type { ServerAccountSettings } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import AccountDetail, { type AccountStatus } from './account-detail.svelte';
	import { libraryFor } from './admin-library.svelte';
	import Monogram from './monogram.svelte';
	import Sheet from './sheet.svelte';
	import ValueRow from './value-row.svelte';
	import FieldRow from './field-row.svelte';
	import InlineConfirm from './inline-confirm.svelte';
	import PageCard from './page-card.svelte';
	import SettingRow from './setting-row.svelte';

	interface Props {
		settings: ServerSettings;
		version: number;
		onaddaccount: () => void;
		onremoveaccount: (index: number) => void;
		/** IMMICHFRAME_ADMIN_PASSWORD is set on the server, so the password here has no effect. */
		passwordFromEnvironment?: boolean;
	}

	let {
		settings,
		version,
		onaddaccount,
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
	const accountName = (_account: ServerAccountSettings, i: number) =>
		libraries[i]?.userName || `Account ${i + 1}`;

	async function test(i: number) {
		const account = settings.Accounts?.[i];
		if (!account) return;
		statuses[i] = { testing: true };
		try {
			const res = await adminApi.testAccount(JSON.parse(JSON.stringify(account)));
			statuses[i] =
				res.status === 200
					? {
							testing: false,
							success: !!res.data.success,
							message: res.data.message ?? '',
							version: res.data.version
						}
					: { testing: false, success: false, message: 'The test request failed.' };
		} catch {
			statuses[i] = {
				testing: false,
				success: false,
				message: 'Couldn’t reach the ImmichFrame server.'
			};
		}
	}

	onMount(() => {
		(settings.Accounts ?? []).forEach((account, i) => {
			if (account.immichServerUrl && (account.apiKey || account.apiKeyFile)) test(i);
		});
	});

	function addAndOpen() {
		onaddaccount();
		openAccount = (settings.Accounts?.length ?? 1) - 1;
	}

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
				<Monogram name={libraries[index]?.userName || String(index + 1)} avatarOf={index} />
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
			type="button"
			class="border-sep flex min-h-11 w-full items-center border-t py-2 text-left text-[0.9375rem] text-primary first:border-t-0 md:text-[0.875rem]"
			onclick={addAndOpen}
		>
			Add Immich Account…
		</button>
	</PageCard>

	{#if openAccount !== null && settings.Accounts?.[openAccount]}
		{#key `${version}-${openAccount}`}
			<Sheet
				title={accountName(settings.Accounts[openAccount], openAccount)}
				returnFocusTo={`account-row-${openAccount}`}
				onclose={() => (openAccount = null)}
			>
				<AccountDetail
					account={settings.Accounts[openAccount]}
					name={accountName(settings.Accounts[openAccount], openAccount)}
					index={openAccount}
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
						<label class="flex items-center gap-3 text-sm">
							Download again after
							<span class="w-24">
								<NumberInput
									value={get('renewImagesDuration')}
									min={1}
									onchange={(e) =>
										(general.renewImagesDuration = Number(
											(e.currentTarget as HTMLInputElement).value
										))}
								/>
							</span>
							days
						</label>
					{/if}
				</SettingRow>
				<SettingRow
					title="Refresh albums and people"
					description="How often the frame re-reads what's in them"
				>
					{#snippet control()}
						<span class="w-24">
							<NumberInput
								value={get('refreshAlbumPeopleInterval')}
								min={0}
								onchange={(e) =>
									(general.refreshAlbumPeopleInterval = Number(
										(e.currentTarget as HTMLInputElement).value
									))}
							/>
						</span>
						<span class="text-label-2 text-[0.875rem]">hours</span>
					{/snippet}
				</SettingRow>
				<SettingRow
					title="Webhook"
					description="Called each time a screen shows a photo, for home automation"
				>
					<Input
						placeholder="https://…"
						aria-label="Webhook address"
						value={general.webhook ?? ''}
						oninput={(e) => (general.webhook = e.currentTarget.value)}
					/>
				</SettingRow>
			</div>
		{/if}
	</PageCard>
</div>
