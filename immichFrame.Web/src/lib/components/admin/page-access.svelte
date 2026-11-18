<script lang="ts">
	import Btn from './btn.svelte';
	import Toggle from './toggle.svelte';
	import { Input, NumberInput, PasswordInput } from '@immich/ui';
	import Glyph from './glyph.svelte';
	import type { ServerSettings } from '$lib/immichFrameApi';
	import { effective, generalDefaults } from './admin-defaults';
	import AccountConnection from './account-connection.svelte';
	import CopyField from './copy-field.svelte';
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
		title="Immich Accounts"
		footer="The frame only reads from Immich. Create a key under Account Settings → API Keys; read access to assets, albums, people, memories and tags is enough."
	>
		{#key version}
			{#each settings.Accounts ?? [] as account, index (index)}
				<AccountConnection {account} {index} onremove={() => onremoveaccount(index)} />
			{/each}
		{/key}
		{#if !(settings.Accounts ?? []).length}
			<p class="text-label-2 py-3 text-[0.875rem]">
				No accounts yet, so the frame has nothing to show.
			</p>
		{/if}
		<button
			type="button"
			class="border-sep flex min-h-11 w-full items-center border-t py-2 text-left text-[0.9375rem] text-primary first:border-t-0 md:text-[0.875rem]"
			onclick={onaddaccount}
		>
			Add Immich Account…
		</button>
	</PageCard>

	<PageCard
		title="Frame Secret"
		footer={general.authenticationSecret
			? 'Screens send this to show photos.'
			: 'There’s no secret, so anyone who can reach the frame can see the photos.'}
	>
		{#if general.authenticationSecret}
			<div class="py-3">
				<CopyField label="Secret" value={general.authenticationSecret} secret />
			</div>
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
			<div
				class="border-sep flex flex-wrap gap-2 py-3 {general.authenticationSecret
					? 'border-t'
					: ''}"
			>
				<Btn
					id="secret-new"
					size="small"
					variant="outline"
					color="secondary"
					onclick={() => (secretAction = 'new')}
				>
					{general.authenticationSecret ? 'Create New Secret…' : 'Create Secret…'}
				</Btn>
				{#if general.authenticationSecret}
					<Btn size="small" variant="outline" color="danger" onclick={() => (secretAction = 'off')}
						>Turn Off Secret…</Btn
					>
				{/if}
			</div>
		{/if}
	</PageCard>

	<PageCard
		title="Admin Password"
		footer={passwordFromEnvironment
			? 'Set by IMMICHFRAME_ADMIN_PASSWORD on the server. Change it there and restart ImmichFrame.'
			: 'For this page only. Screens never need it.'}
	>
		{#if passwordFromEnvironment}
			<div class="flex min-h-11 items-center justify-between py-2">
				<span class="text-[0.875rem]">Password</span>
				<span class="text-label-2 text-[0.875rem]">Set on the server</span>
			</div>
		{:else}
			<form
				class="grid items-start gap-3 py-3 sm:grid-cols-[1fr_1fr_auto]"
				onsubmit={(e) => {
					e.preventDefault();
					setPassword();
				}}
			>
				<label class="text-[0.8125rem]">
					<span class="mb-1 block font-medium">New Password</span>
					<PasswordInput autocomplete="new-password" bind:value={newPassword} />
					{#if newPassword.length > 0 && newPassword.length < 8}
						<span class="text-label-2 mt-1 block text-[0.75rem]">At least 8 characters</span>
					{/if}
				</label>
				<label class="text-[0.8125rem]">
					<span class="mb-1 block font-medium">Confirm Password</span>
					<PasswordInput autocomplete="new-password" bind:value={confirmPassword} />
					{#if mismatch}<span class="mt-1 block text-[0.75rem] text-[#d70015] dark:text-[#ff6961]"
							>Passwords don't match</span
						>{/if}
				</label>
				<Btn
					type="submit"
					size="small"
					variant="outline"
					color="secondary"
					class="sm:mt-6"
					disabled={!canChange}>Change</Btn
				>
			</form>
			{#if passwordReady}
				<p class="text-label-2 pb-3 text-[0.8125rem]">Ready. It takes effect when you save.</p>
			{/if}
		{/if}
	</PageCard>

	<PageCard title="Advanced">
		<button
			type="button"
			class="flex min-h-11 w-full items-center justify-between gap-3 py-2 text-left focus-visible:outline-2 focus-visible:outline-primary"
			aria-expanded={advancedOpen}
			onclick={() => (advancedOpen = !advancedOpen)}
		>
			<span>
				<span class="block text-[0.9375rem] md:text-[0.875rem]">Cache, Refresh and Webhook</span>
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
