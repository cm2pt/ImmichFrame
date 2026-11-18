<script lang="ts">
	import { onMount } from 'svelte';
	import { mdiConnection, mdiDelete } from '@mdi/js';
	import { Avatar, Button, Text } from '@immich/ui';
	import type { ServerAccountSettings } from '$lib/immichFrameApi';
	import * as adminApi from '$lib/services/admin';
	import { accountFields } from './admin-fields';
	import { peekLibrary } from './admin-library.svelte';
	import SettingField from './setting-field.svelte';

	interface Props {
		account: ServerAccountSettings;
		index: number;
		onremove: () => void;
	}

	let { account, index, onremove }: Props = $props();

	const avatarColors = ['primary', 'orange', 'green', 'purple', 'pink', 'blue'] as const;
	const connectionFields = accountFields.filter((f) =>
		['immichServerUrl', 'apiKey', 'apiKeyFile'].includes(f.key)
	);

	let name = $derived(peekLibrary(account)?.userName || `Account ${index + 1}`);
	let avatarName = $derived(peekLibrary(account)?.userName || String(index + 1));
	let testing = $state(false);
	let result: { success: boolean; message: string; version?: string | null } | null = $state(null);

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
					: { success: false, message: 'Test request failed.' };
		} catch {
			result = { success: false, message: 'Test request failed.' };
		} finally {
			testing = false;
		}
	}

	onMount(() => {
		if (account.immichServerUrl && (account.apiKey || account.apiKeyFile)) test();
	});
</script>

<div class="border-t border-light-200 py-5 first:border-t-0">
	<div class="flex items-start gap-4">
		<Avatar name={avatarName} color={avatarColors[index % avatarColors.length]} />
		<div class="min-w-0 flex-1">
			<div class="flex flex-wrap items-center gap-2">
				<Text fontWeight="semi-bold">{name}</Text>
				{#if testing}
					<span class="rounded-full bg-subtle px-2.5 py-0.5 text-xs">Checking…</span>
				{:else if result}
					<span
						class="rounded-full px-2.5 py-0.5 text-xs font-medium {result.success
							? 'bg-success/15 text-success'
							: 'bg-danger/15 text-danger'}"
					>
						{result.success ? `Connected · Immich v${result.version}` : 'Not connected'}
					</span>
				{/if}
			</div>
			{#if result && !result.success}
				<Text size="small" color="danger" class="mt-1">{result.message}</Text>
			{/if}
			<div class="mt-2 grid gap-x-6 sm:grid-cols-2">
				{#each connectionFields as field (field.key)}
					<SettingField {field} target={account as Record<string, unknown>} />
				{/each}
			</div>
		</div>
		<div class="flex flex-col gap-2">
			<Button
				size="small"
				variant="outline"
				leadingIcon={mdiConnection}
				loading={testing}
				onclick={test}
			>
				Test
			</Button>
			<Button
				size="small"
				variant="ghost"
				color="danger"
				leadingIcon={mdiDelete}
				onclick={onremove}
			>
				Remove
			</Button>
		</div>
	</div>
</div>
