<script lang="ts">
	import { onMount } from 'svelte';
	import { get } from 'svelte/store';
	import { AppShell, Card, CardBody, Code, Heading, Text } from '@immich/ui';
	import * as adminApi from '$lib/services/admin';
	import { AdminUiState, type ServerSettings } from '$lib/immichFrameApi';
	import { adminPasswordStore } from '$lib/stores/admin.store';
	import { AdminEditor } from './admin-editor.svelte';
	import AdminEditorShell from './admin-editor-shell.svelte';
	import AdminLogin from './admin-login.svelte';
	import AdminSetup from './admin-setup.svelte';

	type PageState = 'loading' | 'setup' | 'disabled' | 'file' | 'login' | 'editor' | 'error';

	let pageState: PageState = $state('loading');
	const editor = new AdminEditor();
	let loginError = $state('');
	let setupError = $state('');
	let saving = $state(false);
	let saveError = $state('');
	let saveWarnings: string[] = $state([]);
	let savedAt = $state(0);

	onMount(loadStatus);

	async function loadStatus() {
		try {
			const res = await adminApi.getStatus();
			if (res.status != 200) {
				pageState = 'error';
				return;
			}
			if (res.data.state === AdminUiState.Setup) {
				pageState = 'setup';
				return;
			}
			if (res.data.state === AdminUiState.Disabled) {
				pageState = 'disabled';
				return;
			}
			if (res.data.state === AdminUiState.File) {
				pageState = 'file';
				return;
			}
			if (get(adminPasswordStore)) {
				await loadSettings();
			} else {
				pageState = 'login';
			}
		} catch {
			pageState = 'error';
		}
	}

	// Claims the instance, then signs in with the password just chosen so the user
	// lands straight in the editor to add their first account.
	async function setup(password: string) {
		setupError = '';
		try {
			const res = await adminApi.setup(password);
			if (res.status != 200) {
				setupError = 'Setup failed. Check the server logs and try again.';
				return;
			}
			adminPasswordStore.set(password);
			await loadSettings();
		} catch {
			setupError = 'Setup failed. Is the server reachable?';
		}
	}

	async function loadSettings() {
		try {
			const res = await adminApi.getSettings();
			if (res.status == 200) {
				editor.load(res.data);
				pageState = 'editor';
				loginError = '';
			} else {
				adminPasswordStore.set(null);
				loginError = 'Could not authenticate. Check the admin password.';
				pageState = 'login';
			}
		} catch {
			pageState = 'error';
		}
	}

	function login(password: string) {
		adminPasswordStore.set(password);
		loadSettings();
	}

	function logout() {
		adminPasswordStore.set(null);
		loginError = '';
		pageState = 'login';
	}

	async function save() {
		saving = true;
		saveError = '';
		saveWarnings = [];
		try {
			const sent = editor.snapshot();
			const res = await adminApi.updateSettings(sent as ServerSettings);
			if (res.status == 200) {
				saveWarnings = res.data.warnings ?? [];
				// A new admin password signs this browser in with it.
				if (sent.General?.adminPassword && sent.General.adminPassword !== get(adminPasswordStore)) {
					adminPasswordStore.set(sent.General.adminPassword);
				}
				editor.markSaved();
				savedAt = Date.now();
			} else if (res.status == 401) {
				adminPasswordStore.set(null);
				loginError = 'Session expired. Sign in again.';
				pageState = 'login';
			} else {
				const data = res.data as { detail?: string } | string | null;
				saveError =
					(typeof data === 'object' && data?.detail) ||
					(typeof data === 'string' && data) ||
					'Saving failed.';
			}
		} catch {
			saveError = 'Saving failed. Is the server reachable?';
		} finally {
			saving = false;
		}
	}
</script>

{#if pageState === 'editor'}
	<AdminEditorShell
		{editor}
		{saving}
		{saveError}
		{saveWarnings}
		{savedAt}
		onsave={save}
		onreload={loadSettings}
		onlogout={logout}
	/>
{:else}
	<AppShell class="bg-light px-4">
		<div class="p-4 pb-24">
			{#if pageState === 'loading'}
				<Text color="muted" class="block pt-24 text-center">Loading…</Text>
			{:else if pageState === 'error'}
				<Text color="muted" class="block pt-24 text-center">
					Could not reach the ImmichFrame server. Check the container logs and reload this page.
				</Text>
			{:else if pageState === 'setup'}
				<AdminSetup error={setupError} onsetup={setup} />
			{:else if pageState === 'disabled'}
				<Card class="mx-auto mt-24 max-w-lg">
					<CardBody class="text-center">
						<Heading size="large" class="mb-4">Admin UI is disabled</Heading>
						<Text color="muted">
							This instance is already configured but has no admin password. Set the
							<Code>IMMICHFRAME_ADMIN_PASSWORD</Code> environment variable and restart the container to
							get in.
						</Text>
					</CardBody>
				</Card>
			{:else if pageState === 'file'}
				<Card class="mx-auto mt-24 max-w-lg">
					<CardBody class="text-center">
						<Heading size="large" class="mb-4">Settings come from a config file</Heading>
						<Text color="muted">
							This instance is configured declaratively. The admin UI is disabled. Edit
							<Code>Settings.yml</Code> or <Code>Settings.json</Code> and restart ImmichFrame to apply
							changes.
						</Text>
					</CardBody>
				</Card>
			{:else if pageState === 'login'}
				<AdminLogin error={loginError} onlogin={login} />
			{/if}
		</div>
	</AppShell>
{/if}
