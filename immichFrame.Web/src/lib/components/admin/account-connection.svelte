<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import { mdiConnection, mdiDeleteOutline } from '@mdi/js';
	import { Avatar, Button, Input, PasswordInput, Text } from '@immich/ui';
	import type { ServerAccountSettings } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import { relativeTime } from './admin-format';
	import { peekLibrary } from './admin-library.svelte';
	import Callout from './callout.svelte';
	import InlineConfirm from './inline-confirm.svelte';
	import Segmented from './segmented.svelte';

	interface Props {
		account: ServerAccountSettings;
		index: number;
		onremove: () => void;
	}

	let { account, index, onremove }: Props = $props();

	const avatarColors = ['primary', 'orange', 'green', 'purple', 'pink', 'blue'] as const;
	let userName = $derived(peekLibrary(account)?.userName);
	let name = $derived(userName || `Account ${index + 1}`);

	// Initial values only: the parent remounts this row whenever the settings reload.
	const initial = untrack(() => ({ key: account.apiKey, file: account.apiKeyFile }));
	// One way of giving the key at a time: pasted, or read from a file on the server.
	let keyMode: 'paste' | 'file' = $state(initial.file && !initial.key ? 'file' : 'paste');
	// A key loaded from the server shows as "Saved key", not as dots of its real length.
	const hasSavedKey = !!initial.key;
	let editingKey = $state(!initial.key);

	let testing = $state(false);
	let checkedAt: number | null = $state(null);
	let result: { success: boolean; message: string; version?: string | null } | null = $state(null);
	let confirmRemove = $state(false);

	function setKeyMode(mode: 'paste' | 'file') {
		keyMode = mode;
		if (mode === 'file') account.apiKey = null;
		else account.apiKeyFile = null;
	}

	async function test() {
		testing = true;
		try {
			const res = await adminApi.testAccount($state.snapshot(account) as ServerAccountSettings);
			result =
				res.status === 200
					? {
							success: !!res.data.success,
							message: res.data.message ?? '',
							version: res.data.version
						}
					: { success: false, message: 'The test request failed.' };
		} catch {
			result = { success: false, message: 'Couldn’t reach the ImmichFrame server.' };
		} finally {
			testing = false;
			checkedAt = Date.now();
		}
	}

	onMount(() => {
		if (account.immichServerUrl && (account.apiKey || account.apiKeyFile)) test();
	});
</script>

<div class="border-t border-light-200 py-5 first:border-t-0">
	<div class="flex items-center gap-3">
		<Avatar
			name={userName || String(index + 1)}
			color={avatarColors[index % avatarColors.length]}
		/>
		<div class="min-w-0 flex-1">
			<Text fontWeight="semi-bold">{name}</Text>
			<Text
				size="small"
				class={result && !result.success ? 'text-danger-700 dark:text-danger-400' : 'text-dark/70'}
			>
				{#if testing}
					Checking the connection…
				{:else if result?.success}
					Connected&nbsp;·&ensp;Immich {result.version}{checkedAt
						? `\u00a0·\u2002checked ${relativeTime(checkedAt)}`
						: ''}
				{:else if result}
					Not connected
				{:else}
					Not checked yet
				{/if}
			</Text>
		</div>
	</div>

	{#if result && !result.success}
		<div class="mt-3"><Callout tone="danger">{result.message}</Callout></div>
	{/if}

	<div class="mt-4 grid gap-4 sm:grid-cols-2">
		<label class="text-sm">
			<span class="mb-1 block font-medium">Server address</span>
			<Input
				placeholder="http://immich:2283"
				value={account.immichServerUrl ?? ''}
				oninput={(e) => (account.immichServerUrl = e.currentTarget.value)}
			/>
		</label>
		<div class="text-sm">
			<div class="mb-1 flex items-center justify-between gap-2">
				<span class="font-medium">API key</span>
				<Segmented
					label="Where the key comes from"
					value={keyMode}
					options={[
						{ value: 'paste', label: 'Paste' },
						{ value: 'file', label: 'From file' }
					]}
					onchange={setKeyMode}
				/>
			</div>
			{#if keyMode === 'file'}
				<Input
					placeholder="/run/secrets/immich-api-key"
					aria-label="Key file on the server"
					value={account.apiKeyFile ?? ''}
					oninput={(e) => (account.apiKeyFile = e.currentTarget.value)}
				/>
			{:else if hasSavedKey && !editingKey}
				<div
					class="flex items-center justify-between rounded-lg bg-subtle/50 px-4 py-2.5 dark:bg-light-200"
				>
					<span class="text-dark/70">Saved key</span>
					<Button size="tiny" variant="ghost" onclick={() => (editingKey = true)}>Replace</Button>
				</div>
			{:else}
				<PasswordInput
					autocomplete="off"
					placeholder="Paste the key from Immich"
					aria-label="API key"
					value={account.apiKey ?? ''}
					oninput={(e) => (account.apiKey = e.currentTarget.value)}
				/>
			{/if}
		</div>
	</div>

	{#if confirmRemove}
		<InlineConfirm
			message={`Remove ${name}? Its photos leave the frame when you save.`}
			confirmLabel="Remove"
			danger
			returnFocusTo={`remove-account-${index}`}
			onconfirm={onremove}
			oncancel={() => (confirmRemove = false)}
		/>
	{:else}
		<div class="mt-4 flex justify-end gap-2">
			<Button
				size="small"
				variant="ghost"
				color="secondary"
				id={`remove-account-${index}`}
				leadingIcon={mdiDeleteOutline}
				onclick={() => (confirmRemove = true)}
			>
				Remove…
			</Button>
			<Button
				size="small"
				variant="outline"
				color="secondary"
				leadingIcon={mdiConnection}
				loading={testing}
				onclick={test}
			>
				Test connection
			</Button>
		</div>
	{/if}
</div>
