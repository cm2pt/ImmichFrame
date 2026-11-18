<script lang="ts">
	import { mdiLogin } from '@mdi/js';
	import { Alert, Button, Card, CardBody, Field, PasswordInput, Text } from '@immich/ui';
	import AdminBrand from './admin-brand.svelte';

	interface Props {
		error?: string;
		onlogin: (password: string) => void;
	}

	let { error = '', onlogin }: Props = $props();
	let password = $state('');
	let empty = $state(false);

	function submit(e: SubmitEvent) {
		e.preventDefault();
		empty = !password;
		if (empty) return;
		onlogin(password);
	}
</script>

<Card class="mx-auto mt-[18vh] max-w-sm shadow-xl">
	<CardBody>
		<form class="flex flex-col gap-4" onsubmit={submit}>
			<AdminBrand subtitle="Admin sign-in" />

			<Field label="Admin password">
				<PasswordInput autofocus bind:value={password} />
			</Field>

			{#if error}
				<Alert color="danger">{error}</Alert>
			{/if}

			{#if empty}
				<Text size="small" class="text-danger-700 dark:text-danger-400"
					>Enter the admin password.</Text
				>
			{/if}

			<Button type="submit" leadingIcon={mdiLogin} fullWidth>Sign in</Button>
			<Text size="small" class="text-center text-dark/60">
				Forgot it? Set <code>IMMICHFRAME_ADMIN_PASSWORD</code> on the server and restart.
			</Text>
		</form>
	</CardBody>
</Card>
