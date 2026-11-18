<script lang="ts">
	import Btn from './btn.svelte';
	import Toggle from './toggle.svelte';
	import { mdiChevronDown, mdiPlus } from '@mdi/js';
	import { Icon, Input, NumberInput, PasswordInput, Text } from '@immich/ui';
	import type { ServerSettings } from '$lib/immichFrameApi';
	import { effective, generalDefaults } from './admin-defaults';
	import AccountConnection from './account-connection.svelte';
	import Callout from './callout.svelte';
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

<div class="flex flex-col gap-6">
	<PageCard
		title="Immich accounts"
		description="The frame only reads from Immich. Each account's key stays on the server."
	>
		{#key version}
			{#each settings.Accounts ?? [] as account, index (index)}
				<AccountConnection {account} {index} onremove={() => onremoveaccount(index)} />
			{/each}
		{/key}
		{#if !(settings.Accounts ?? []).length}
			<div class="my-3 rounded-2xl border border-dashed border-light-300 p-6 text-center">
				<Text fontWeight="semi-bold">No accounts yet</Text>
				<Text size="small" class="mt-1 text-label-2"
					>The frame has nothing to show until you add one.</Text
				>
			</div>
		{/if}
		<div
			class="mt-2 flex flex-wrap items-center justify-between gap-4 border-t border-light-200 pt-4"
		>
			<Text size="small" class="max-w-md min-w-48 flex-1 text-label-2">
				Create a key in Immich under Account Settings → API Keys. Read access to assets, albums,
				people, memories and tags is enough.
			</Text>
			<Btn leadingIcon={mdiPlus} onclick={onaddaccount}>Add account</Btn>
		</div>
	</PageCard>

	<PageCard
		title="Frame secret"
		description="Screens send this to show photos. Without one, anyone who can reach the frame can see them."
	>
		<div class="py-2">
			{#if general.authenticationSecret}
				<CopyField label="Secret" value={general.authenticationSecret} secret />
			{:else}
				<Callout>The frame has no secret, so anyone who can reach it can see the photos.</Callout>
			{/if}
		</div>

		{#if secretAction}
			<InlineConfirm
				message={secretAction === 'new'
					? 'After you save, every screen needs the new secret before it shows photos again.'
					: 'After you save, anyone who can reach the frame can see the photos.'}
				confirmLabel={secretAction === 'new' ? 'Create secret' : 'Turn off'}
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
		{:else}
			<div class="mt-2 flex flex-wrap gap-2">
				<Btn
					id="secret-new"
					size="small"
					variant="outline"
					color="secondary"
					onclick={() => (secretAction = 'new')}
				>
					{general.authenticationSecret ? 'Create a new secret…' : 'Create a secret…'}
				</Btn>
				{#if general.authenticationSecret}
					<Btn size="small" variant="ghost" color="danger" onclick={() => (secretAction = 'off')}>
						Turn off secret…
					</Btn>
				{/if}
			</div>
		{/if}
	</PageCard>

	<PageCard title="Admin password" description="For this page only. Screens never need it.">
		{#if passwordFromEnvironment}
			<div class="py-2">
				<Callout tone="info">
					Set by <code>IMMICHFRAME_ADMIN_PASSWORD</code> on the server. Change it there and restart ImmichFrame.
				</Callout>
			</div>
		{:else}
			<form
				class="grid items-start gap-4 py-2 sm:grid-cols-[1fr_1fr_auto]"
				onsubmit={(e) => {
					e.preventDefault();
					setPassword();
				}}
			>
				<label class="text-sm">
					<span class="mb-1 block font-medium">New password</span>
					<PasswordInput autocomplete="new-password" bind:value={newPassword} />
					{#if newPassword.length > 0 && newPassword.length < 8}
						<span class="mt-1 block text-label-2">At least 8 characters</span>
					{/if}
				</label>
				<label class="text-sm">
					<span class="mb-1 block font-medium">Confirm password</span>
					<PasswordInput autocomplete="new-password" bind:value={confirmPassword} />
					{#if mismatch}<span class="mt-1 block text-danger-700 dark:text-danger-400"
							>Passwords don't match</span
						>{/if}
				</label>
				<Btn
					type="submit"
					variant="outline"
					color="secondary"
					class="sm:mt-6"
					disabled={!canChange}
				>
					Change
				</Btn>
			</form>
			{#if passwordReady}
				<Text size="small" class="mt-1 text-dark/80">✓ Ready. It takes effect when you save.</Text>
			{/if}
		{/if}
	</PageCard>

	<section class="rounded-2xl border border-light-200">
		<button
			type="button"
			class="flex w-full items-center justify-between gap-3 rounded-2xl px-5 py-4 text-left focus-visible:outline-2 focus-visible:outline-primary"
			aria-expanded={advancedOpen}
			onclick={() => (advancedOpen = !advancedOpen)}
		>
			<span>
				<span class="block font-semibold">Advanced</span>
				<span class="block text-sm text-label-2">Image cache, album refresh and webhook</span>
			</span>
			<span class="transition-transform {advancedOpen ? 'rotate-180' : ''}"
				><Icon icon={mdiChevronDown} size="22" /></span
			>
		</button>
		{#if advancedOpen}
			<div class="px-5 pb-3">
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
						<Text size="small">hours</Text>
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
	</section>
</div>
