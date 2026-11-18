<script lang="ts">
	import Btn from './btn.svelte';
	import { mdiDownload, mdiUpload } from '@mdi/js';
	import { Text, Heading } from '@immich/ui';
	import type { ServerSettings } from '$lib/immichFrameApi';
	import { accountSecrets, generalSecrets } from './admin-defaults';

	interface Props {
		/** The last saved settings (a download never includes unsaved edits). */
		saved: ServerSettings;
		/** Loads a restored file into the editor as unsaved changes. */
		onrestore: (settings: ServerSettings) => void;
	}

	let { saved, onrestore }: Props = $props();

	let message = $state('');
	let failed = $state(false);
	let input: HTMLInputElement | undefined = $state();

	function download() {
		const copy: ServerSettings = JSON.parse(JSON.stringify(saved));
		for (const key of generalSecrets)
			delete (copy.General as Record<string, unknown> | undefined)?.[key];
		for (const account of copy.Accounts ?? []) {
			for (const key of accountSecrets) delete (account as Record<string, unknown>)[key];
		}
		const blob = new Blob([JSON.stringify(copy, null, 2)], { type: 'application/json' });
		const a = document.createElement('a');
		a.href = URL.createObjectURL(blob);
		a.download = `immichframe-settings-${new Date().toISOString().slice(0, 10)}.json`;
		a.click();
		URL.revokeObjectURL(a.href);
	}

	async function restore(file: File) {
		failed = false;
		try {
			const parsed = JSON.parse(await file.text());
			if (typeof parsed !== 'object' || parsed === null || (!parsed.General && !parsed.Accounts)) {
				throw new Error('not a settings file');
			}
			const restored: ServerSettings = {
				General: { ...(parsed.General ?? {}) },
				Accounts: [...(parsed.Accounts ?? [])]
			};
			// Secrets aren't in downloads: keep the current ones (accounts matched by server address).
			for (const key of generalSecrets) {
				const g = restored.General as Record<string, unknown>;
				if (!g[key]) g[key] = (saved.General as Record<string, unknown> | undefined)?.[key];
			}
			for (const account of restored.Accounts ?? []) {
				const match = (saved.Accounts ?? []).find(
					(a) => a.immichServerUrl === account.immichServerUrl
				);
				for (const key of accountSecrets) {
					const a = account as Record<string, unknown>;
					if (!a[key] && !account.apiKeyFile && match)
						a[key] = (match as Record<string, unknown>)[key];
				}
			}
			onrestore(restored);
			message = 'Loaded. Review the changes, then Save.';
		} catch {
			failed = true;
			message = 'That file isn’t an ImmichFrame settings download.';
		} finally {
			if (input) input.value = '';
		}
	}
</script>

<div class="bg-group rounded-2xl shadow-[0_1px_2px_rgba(0,0,0,0.04)]">
	<div class="p-4">
		<Heading tag="h2" size="tiny" fontWeight="semi-bold">Backup</Heading>
		<Text size="small" class="text-label-2 mb-3">
			Download every setting as one file (secrets left out), or restore one.
		</Text>
		<div class="flex flex-wrap gap-2">
			<Btn
				size="small"
				variant="outline"
				color="secondary"
				leadingIcon={mdiDownload}
				onclick={download}>Download settings</Btn
			>
			<Btn
				size="small"
				variant="outline"
				color="secondary"
				leadingIcon={mdiUpload}
				onclick={() => input?.click()}>Restore…</Btn
			>
			<input
				bind:this={input}
				type="file"
				accept="application/json,.json"
				class="hidden"
				onchange={(e) => {
					const file = e.currentTarget.files?.[0];
					if (file) restore(file);
				}}
			/>
		</div>
		{#if message}<Text size="small" color={failed ? 'danger' : 'success'} class="mt-2"
				>{message}</Text
			>{/if}
	</div>
</div>
