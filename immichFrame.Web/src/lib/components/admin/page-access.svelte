<script lang="ts">
	import { mdiChevronDown, mdiPlus } from '@mdi/js';
	import { Button, Icon, Input, NumberInput, PasswordInput, Switch, Text } from '@immich/ui';
	import type { ServerSettings } from '$lib/immichFrameApi';
	import { effective, generalDefaults } from './admin-defaults';
	import AccountConnection from './account-connection.svelte';
	import CopyField from './copy-field.svelte';
	import PageCard from './page-card.svelte';
	import SettingRow from './setting-row.svelte';

	interface Props {
		settings: ServerSettings;
		version: number;
		onaddaccount: () => void;
		onremoveaccount: (index: number) => void;
	}

	let { settings, version, onaddaccount, onremoveaccount }: Props = $props();

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
				<Text size="small" class="mt-1 text-dark/70"
					>The frame has nothing to show until you add one.</Text
				>
			</div>
		{/if}
		<div
			class="mt-2 flex flex-wrap items-center justify-between gap-4 border-t border-light-200 pt-4"
		>
			<Text size="small" class="max-w-md min-w-48 flex-1 text-dark/70">
				Create a key in Immich under Account Settings → API Keys. Read access to assets, albums,
				people, memories and tags is enough.
			</Text>
			<Button leadingIcon={mdiPlus} onclick={onaddaccount}>Add account</Button>
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
				<div class="rounded-xl bg-warning/10 px-4 py-3 text-sm text-warning">
					The frame has no secret.
				</div>
			{/if}
		</div>

		{#if secretAction}
			<div
				class="mt-2 rounded-xl bg-subtle/50 p-4 dark:bg-light-100"
				role="alertdialog"
				aria-label="Confirm"
			>
				<Text size="small">
					{secretAction === 'new'
						? general.authenticationSecret
							? 'After you save, every screen needs the new secret before it shows photos again.'
							: 'After you save, every screen needs this secret before it shows photos again.'
						: 'After you save, anyone who can reach the frame can see the photos.'}
				</Text>
				<div class="mt-3 flex justify-end gap-2">
					<Button
						size="small"
						variant="ghost"
						color="secondary"
						onclick={() => (secretAction = null)}>Cancel</Button
					>
					{#if secretAction === 'new'}
						<Button size="small" onclick={newSecret}>Create secret</Button>
					{:else}
						<Button
							size="small"
							color="danger"
							onclick={() => {
								general.authenticationSecret = null;
								secretAction = null;
							}}>Turn off</Button
						>
					{/if}
				</div>
			</div>
		{:else}
			<div class="mt-2 flex flex-wrap gap-2">
				<Button
					size="small"
					variant="outline"
					color="secondary"
					onclick={() => (secretAction = 'new')}
				>
					{general.authenticationSecret ? 'Create a new secret…' : 'Create a secret…'}
				</Button>
				{#if general.authenticationSecret}
					<Button
						size="small"
						variant="ghost"
						color="danger"
						onclick={() => (secretAction = 'off')}
					>
						Turn off secret…
					</Button>
				{/if}
			</div>
		{/if}
	</PageCard>

	<PageCard title="Admin password" description="For this page only. Screens never need it.">
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
					<span class="mt-1 block text-dark/60">At least 8 characters</span>
				{/if}
			</label>
			<label class="text-sm">
				<span class="mb-1 block font-medium">Confirm password</span>
				<PasswordInput autocomplete="new-password" bind:value={confirmPassword} />
				{#if mismatch}<span class="mt-1 block text-danger">Passwords don't match</span>{/if}
			</label>
			<Button
				type="submit"
				variant="outline"
				color="secondary"
				class="sm:mt-6"
				disabled={!canChange}
			>
				Change
			</Button>
		</form>
		{#if passwordReady}
			<Text size="small" color="success" class="mt-1">Ready. It takes effect when you save.</Text>
		{/if}
		<Text size="small" class="mt-2 text-dark/60">
			The <code>IMMICHFRAME_ADMIN_PASSWORD</code> environment variable, when set, always wins over this
			password.
		</Text>
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
				<span class="block text-sm text-dark/70">Image cache, album refresh and webhook</span>
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
						<Switch
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
