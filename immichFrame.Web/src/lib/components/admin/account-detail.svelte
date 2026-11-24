<script lang="ts">
	import { untrack } from 'svelte';
	import type { ServerAccountSettings } from '$lib/immichFrameApi';
	import FieldRow from './field-row.svelte';
	import InlineConfirm from './inline-confirm.svelte';
	import Monogram from './monogram.svelte';
	import { monogramOf } from './admin-library.svelte';
	import PageCard from './page-card.svelte';
	import SettingRow from './setting-row.svelte';
	import Toggle from './toggle.svelte';

	export interface AccountStatus {
		testing: boolean;
		success?: boolean;
		message?: string;
		version?: string | null;
	}

	interface Props {
		account: ServerAccountSettings;
		name: string;
		/** Its place in the saved settings, for the Immich profile picture. */
		index?: number | null;
		/** The Immich user the key belongs to, suggested as the name. */
		immichName?: string | null;
		status: AccountStatus | undefined;
		ontest: () => void;
		onremove: () => void;
	}

	let { account, name, index = null, immichName, status, ontest, onremove }: Props = $props();

	// Initial values only: the sheet is created fresh each time it opens.
	const initial = untrack(() => ({ key: account.apiKey, file: account.apiKeyFile }));
	let useFile = $state(!!initial.file && !initial.key);
	const hasSavedKey = !!initial.key;
	let editingKey = $state(!initial.key);
	let confirmRemove = $state(false);

	function setUseFile(value: boolean) {
		useFile = value;
		if (value) account.apiKey = null;
		else account.apiKeyFile = null;
	}
</script>

<div class="flex flex-col items-center gap-2 pt-1 text-center">
	<Monogram name={monogramOf(name)} size={56} avatarOf={index} />
	<p class="text-[1.0625rem] font-semibold">{name}</p>
	<p class="text-label-2 flex items-center gap-1.5 text-[0.8125rem]">
		{#if status?.testing}
			Checking…
		{:else if status?.success}
			<span class="h-1.5 w-1.5 rounded-full bg-[#30d158]"></span> Connected&nbsp;·&ensp;Immich {status.version}
		{:else if status}
			<span class="h-1.5 w-1.5 rounded-full bg-[#ff3b30]"></span> Not connected
		{/if}
	</p>
</div>

<PageCard footer="Only for this page, e.g. Family or Joana. The frame never shows it.">
	<FieldRow
		title="Name"
		placeholder={immichName || 'Optional'}
		value={account.name}
		oninput={(v) => (account.name = v.trim() ? v : null)}
	/>
</PageCard>

<PageCard footer={status && !status.success && !status.testing ? status.message : undefined}>
	<FieldRow
		title="Server address"
		placeholder="http://immich:2283"
		type="url"
		value={account.immichServerUrl}
		oninput={(v) => (account.immichServerUrl = v)}
	/>
	{#if useFile}
		<FieldRow
			title="Key file"
			placeholder="/run/secrets/immich-api-key"
			value={account.apiKeyFile}
			oninput={(v) => (account.apiKeyFile = v)}
		/>
	{:else if hasSavedKey && !editingKey}
		<div class="border-sep flex min-h-11 items-center justify-between gap-4 border-t py-1">
			<span class="text-[0.9375rem] md:text-[0.875rem]">API key</span>
			<span class="flex items-center gap-3">
				<span class="text-label-2 text-[0.9375rem] md:text-[0.875rem]">Saved</span>
				<button
					type="button"
					class="text-[0.9375rem] text-primary md:text-[0.875rem]"
					onclick={() => (editingKey = true)}>Replace…</button
				>
			</span>
		</div>
	{:else}
		<FieldRow
			title="API key"
			type="password"
			placeholder="Required"
			value={account.apiKey}
			oninput={(v) => (account.apiKey = v)}
		/>
	{/if}
	<SettingRow title="Use key file" description="Read the key from a file on the server instead">
		{#snippet control()}
			<Toggle aria-label="Use key file" checked={useFile} onCheckedChange={setUseFile} />
		{/snippet}
	</SettingRow>
</PageCard>

<PageCard
	footer="Read access to assets, albums, people, memories and tags is enough. Create a key in Immich under Account Settings → API Keys."
>
	<button
		type="button"
		class="flex min-h-11 w-full items-center py-2 text-left text-[0.9375rem] text-primary disabled:opacity-50 md:text-[0.875rem]"
		disabled={status?.testing}
		onclick={ontest}
	>
		{status?.testing ? 'Testing…' : 'Test Connection'}
	</button>
</PageCard>

<PageCard>
	{#if confirmRemove}
		<div class="pb-3">
			<InlineConfirm
				message={`Remove ${name}? Its photos leave the frame when you save.`}
				confirmLabel="Remove"
				danger
				onconfirm={onremove}
				oncancel={() => (confirmRemove = false)}
			/>
		</div>
	{:else}
		<button
			type="button"
			class="flex min-h-11 w-full items-center py-2 text-left text-[0.9375rem] text-[#d70015] md:text-[0.875rem] dark:text-[#ff6961]"
			onclick={() => (confirmRemove = true)}
		>
			Remove Account…
		</button>
	{/if}
</PageCard>
