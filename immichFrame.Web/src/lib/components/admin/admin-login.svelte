<script lang="ts">
	import Glyph from './glyph.svelte';

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

	function focusOnMount(node: HTMLInputElement) {
		node.focus();
	}
</script>

<!-- Like signing in to iCloud.com: the app's icon, a title, one field with its own arrow. -->
<div
	class="mx-auto flex min-h-[80dvh] max-w-sm flex-col items-center justify-center px-4 text-center"
>
	<img src="/logo.svg" alt="" class="h-16 w-16" />
	<h1 class="mt-5 text-[1.75rem] leading-tight font-semibold tracking-[-0.02em]">
		Sign in to ImmichFrame
	</h1>
	<p class="text-label-2 mt-1.5 text-[0.9375rem]">Enter the admin password to change the frame.</p>

	<form class="mt-7 w-full" onsubmit={submit}>
		<div
			class="bg-group flex items-center rounded-[12px] p-1 pl-4 shadow-[0_0_0_1px_rgb(0_0_0/0.12)] focus-within:shadow-[0_0_0_1px_color-mix(in_oklab,var(--if-accent-fill)_70%,transparent)] dark:shadow-[0_0_0_1px_rgb(255_255_255/0.16)]"
		>
			<input
				use:focusOnMount
				type="password"
				autocomplete="current-password"
				placeholder="Admin password"
				aria-label="Admin password"
				bind:value={password}
				class="h-10 min-w-0 flex-1 bg-transparent text-[1.0625rem] outline-none placeholder:text-[#8e8e93]"
			/>
			<button
				type="submit"
				aria-label="Sign in"
				class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full transition {password
					? 'bg-accent'
					: 'bg-fill text-label-2'}"
			>
				<Glyph name="arrowRight" size={18} stroke={2.2} />
			</button>
		</div>
		<p
			class="mt-3 min-h-5 text-[0.8125rem] {error || empty
				? 'text-[#d70015] dark:text-[#ff6961]'
				: ''}"
			role="alert"
		>
			{error || (empty ? 'Enter the admin password.' : '')}
		</p>
	</form>

	<p class="text-label-2 mt-6 text-[0.8125rem]">
		Forgot it? Set IMMICHFRAME_ADMIN_PASSWORD on the server and restart ImmichFrame.
	</p>
</div>
