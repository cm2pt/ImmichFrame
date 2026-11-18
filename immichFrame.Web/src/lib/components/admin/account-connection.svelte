<script lang="ts">
	import Btn from './btn.svelte';
	import { onMount, untrack } from 'svelte';
	import { Input, PasswordInput } from '@immich/ui';
	import Monogram from './monogram.svelte';
	import type { ServerAccountSettings } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import { relativeTime } from './admin-format';
	import { peekLibrary } from './admin-library.svelte';
	import InlineConfirm from './inline-confirm.svelte';
	import Segmented from './segmented.svelte';

	interface Props {
		account: ServerAccountSettings;
		index: number;
		onremove: () => void;
	}

	let { account, index, onremove }: Props = $props();

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

<div class="border-sep border-t py-3.5 first:border-t-0">
	<div class="flex items-center gap-3">
		<Monogram name={userName || String(index + 1)} />
		<div class="min-w-0 flex-1">
			<p class="text-[0.9375rem] font-medium md:text-[0.875rem]">{name}</p>
			<p
				class="flex items-center gap-1.5 text-[0.8125rem] md:text-[0.75rem] {result &&
				!result.success
					? 'text-[#d70015] dark:text-[#ff6961]'
					: 'text-label-2'}"
			>
				{#if testing}
					Checking the connection…
				{:else if result?.success}
					<span class="h-1.5 w-1.5 rounded-full bg-[#30d158]"></span>
					Connected&nbsp;·&ensp;Immich {result.version}{checkedAt
						? `\u00a0·\u2002checked ${relativeTime(checkedAt)}`
						: ''}
				{:else if result}
					<span class="h-1.5 w-1.5 rounded-full bg-[#ff3b30]"></span>
					{result.message}
				{:else}
					Not checked yet
				{/if}
			</p>
		</div>
	</div>

	<div class="mt-3 grid gap-3 sm:grid-cols-2">
		<label class="text-[0.8125rem]">
			<span class="mb-1 block font-medium">Server Address</span>
			<Input
				placeholder="http://immich:2283"
				value={account.immichServerUrl ?? ''}
				oninput={(e) => (account.immichServerUrl = e.currentTarget.value)}
			/>
		</label>
		<div class="text-[0.8125rem]">
			<div class="mb-1 flex items-center justify-between gap-2">
				<span class="font-medium">API Key</span>
				<Segmented
					label="Where the key comes from"
					value={keyMode}
					options={[
						{ value: 'paste', label: 'Paste' },
						{ value: 'file', label: 'From File' }
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
				<div class="flex min-h-10 items-center justify-between md:min-h-9">
					<span class="text-label-2 text-[0.875rem]">Saved</span>
					<Btn size="small" variant="ghost" color="primary" onclick={() => (editingKey = true)}
						>Replace…</Btn
					>
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
		<div class="mt-3 flex justify-end gap-2">
			<Btn
				size="small"
				variant="outline"
				color="danger"
				id={`remove-account-${index}`}
				onclick={() => (confirmRemove = true)}
			>
				Remove…
			</Btn>
			<Btn size="small" variant="outline" color="secondary" loading={testing} onclick={test}
				>Test Connection</Btn
			>
		</div>
	{/if}
</div>
