<script lang="ts">
	import { mdiPlus, mdiRefresh } from '@mdi/js';
	import { Button, Input, NumberInput, PasswordInput, Switch, Text } from '@immich/ui';
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

	function newSecret() {
		const bytes = new Uint8Array(18);
		crypto.getRandomValues(bytes);
		general.authenticationSecret = btoa(String.fromCharCode(...bytes))
			.replace(/\+/g, '-')
			.replace(/\//g, '_');
	}

	let newPassword = $state('');
	let confirmPassword = $state('');
	let passwordNote = $state('');
	function setPassword() {
		if (newPassword.length < 8) {
			passwordNote = 'Use at least 8 characters.';
			return;
		}
		if (newPassword !== confirmPassword) {
			passwordNote = 'The two passwords don’t match.';
			return;
		}
		general.adminPassword = newPassword;
		newPassword = confirmPassword = '';
		passwordNote =
			'New password ready. It takes effect when you save; sign in again with it afterwards.';
	}
</script>

<div class="flex flex-col gap-6">
	<PageCard
		title="Immich accounts"
		description="The frame only reads. Each account uses its own API key, which never leaves the server."
		id="accounts"
	>
		{#key version}
			{#each settings.Accounts ?? [] as account, index (index)}
				<AccountConnection {account} {index} onremove={() => onremoveaccount(index)} />
			{/each}
		{/key}
		{#if !(settings.Accounts ?? []).length}
			<Text color="muted" size="small" class="py-3">
				No accounts yet, so the frame has nothing to show. Add your first Immich account.
			</Text>
		{/if}
		<div
			class="mt-2 flex flex-wrap items-center justify-between gap-4 border-t border-light-200 pt-4"
		>
			<div class="min-w-48 flex-1">
				<Text fontWeight="semi-bold">Add an account</Text>
				<Text size="small" color="muted">
					In Immich: Account Settings → API Keys → New API key. Read permissions for assets, albums,
					people, memories and tags are enough.
				</Text>
			</div>
			<Button leadingIcon={mdiPlus} onclick={onaddaccount}>Add account</Button>
		</div>
	</PageCard>

	<PageCard
		title="Frame secret"
		description="Every screen sends it. Without it, the frame shows nothing. Leave it empty only on a network you trust."
		id="secret"
	>
		<div class="flex flex-wrap items-end gap-3 py-2">
			<div class="min-w-64 flex-1">
				{#if general.authenticationSecret}
					<CopyField label="Secret" value={general.authenticationSecret} secret />
				{:else}
					<Text size="small" color="warning"
						>No secret set: anyone who can reach the frame sees the photos.</Text
					>
				{/if}
			</div>
			<Button
				variant="outline"
				color={general.authenticationSecret ? 'danger' : 'primary'}
				leadingIcon={mdiRefresh}
				onclick={newSecret}
			>
				{general.authenticationSecret ? 'New secret' : 'Create a secret'}
			</Button>
			{#if general.authenticationSecret}
				<Button
					variant="ghost"
					color="secondary"
					onclick={() => (general.authenticationSecret = null)}>Remove</Button
				>
			{/if}
		</div>
		{#if general.authenticationSecret}
			<Text size="small" color="muted"
				>After a new secret is saved, each screen needs it once (Screens → Add a screen shows it).</Text
			>
		{/if}
	</PageCard>

	<PageCard
		title="Admin password"
		description="For this page only. Screens never need it."
		id="password"
	>
		<form
			class="grid items-end gap-4 py-2 sm:grid-cols-[1fr_1fr_auto]"
			onsubmit={(e) => {
				e.preventDefault();
				setPassword();
			}}
		>
			<label class="text-sm">
				<span class="mb-1 block font-medium">New password</span>
				<PasswordInput autocomplete="new-password" bind:value={newPassword} />
			</label>
			<label class="text-sm">
				<span class="mb-1 block font-medium">Repeat it</span>
				<PasswordInput autocomplete="new-password" bind:value={confirmPassword} />
			</label>
			<Button type="submit" variant="outline">Change</Button>
		</form>
		{#if passwordNote}<Text size="small" class="mt-1">{passwordNote}</Text>{/if}
		<Text size="small" color="muted" class="mt-2">
			If the IMMICHFRAME_ADMIN_PASSWORD environment variable is set, it always wins and this one is
			ignored.
		</Text>
	</PageCard>

	<PageCard
		title="Server"
		description="Caching and integrations. The defaults suit most setups."
		id="server"
	>
		<SettingRow
			title="Keep images on disk"
			description="Downloads photos once and serves them from a local cache"
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
					Renew cached images after
					<span class="w-24"
						><NumberInput
							value={get('renewImagesDuration')}
							min={1}
							onchange={(e) =>
								(general.renewImagesDuration = Number((e.currentTarget as HTMLInputElement).value))}
						/></span
					>
					days
				</label>
			{/if}
		</SettingRow>
		<SettingRow
			title="Refresh albums and people"
			description="How often the frame re-reads album and person contents"
		>
			{#snippet control()}
				<span class="w-24"
					><NumberInput
						value={get('refreshAlbumPeopleInterval')}
						min={0}
						onchange={(e) =>
							(general.refreshAlbumPeopleInterval = Number(
								(e.currentTarget as HTMLInputElement).value
							))}
					/></span
				>
				<Text size="small">hours</Text>
			{/snippet}
		</SettingRow>
		<SettingRow
			title="Webhook"
			description="Called when a screen requests a photo (for home automation)"
		>
			<Input
				placeholder="https://…"
				value={general.webhook ?? ''}
				oninput={(e) => (general.webhook = e.currentTarget.value)}
			/>
		</SettingRow>
	</PageCard>
</div>
