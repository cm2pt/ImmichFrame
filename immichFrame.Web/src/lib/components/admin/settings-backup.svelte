<script lang="ts">
	import Btn from './btn.svelte';
	import PageCard from './page-card.svelte';
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
			message = 'That file isn’t an ImmichFrame settings download.';
		} finally {
			if (input) input.value = '';
		}
	}
</script>

<PageCard
	title="Backup"
	footer={message || 'Download every setting as one file, without secrets, or restore one.'}
>
	<div class="flex flex-wrap gap-2 py-3">
		<Btn size="small" variant="outline" color="secondary" onclick={download}>Download Settings</Btn>
		<Btn size="small" variant="outline" color="secondary" onclick={() => input?.click()}
			>Restore…</Btn
		>
		<input
			bind:this={input}
			type="file"
			accept="application/json,.json"
			class="hidden"
			aria-label="Settings file to restore"
			onchange={(e) => {
				const file = e.currentTarget.files?.[0];
				if (file) restore(file);
			}}
		/>
	</div>
</PageCard>
