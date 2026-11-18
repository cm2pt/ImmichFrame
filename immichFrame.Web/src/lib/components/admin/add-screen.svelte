<script lang="ts">
	import QrCode from '@castlenine/svelte-qrcode';
	import { Input, Select, Text } from '@immich/ui';
	import Callout from './callout.svelte';
	import CopyField from './copy-field.svelte';
	import PageCard from './page-card.svelte';

	interface Props {
		/** The saved frame secret (screens use the saved one, not an unsaved edit). */
		secret: string | null | undefined;
		ongotoaccess: () => void;
	}

	let { secret, ongotoaccess }: Props = $props();

	type Device = 'appletv' | 'ios' | 'android' | 'browser';
	let device: Device = $state('appletv');
	let screenName = $state('');

	const origin = typeof window === 'undefined' ? '' : window.location.origin;
	const isLocal = /\/\/(localhost|127\.0\.0\.1|\[::1\])(:|\/|$)/.test(origin);

	let frameUrl = $derived.by(() => {
		// Rebuilt on every change, so a plain (non-reactive) instance is right here.
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const params = new URLSearchParams();
		if (screenName.trim()) params.set('client', screenName.trim());
		if (secret) params.set('authsecret', secret);
		const query = params.toString();
		return `${origin}/${query ? '?' + query : ''}`;
	});

	const devices: { value: Device; label: string }[] = [
		{ value: 'appletv', label: 'Apple TV' },
		{ value: 'ios', label: 'iPad or iPhone' },
		{ value: 'android', label: 'Android or Frameo' },
		{ value: 'browser', label: 'Any browser' }
	];

	let usesApp = $derived(device === 'appletv' || device === 'android');
</script>

<PageCard title="Add a screen" description="Choose a device to see the steps.">
	<!-- Tabs where there's room, a menu on phones. -->
	<div class="mt-2 sm:hidden">
		<Select options={devices} value={device} onChange={(v) => (device = v as Device)} />
	</div>
	<div
		role="tablist"
		aria-label="Device"
		class="mt-2 hidden gap-1 border-b border-light-200 sm:flex"
	>
		{#each devices as tab (tab.value)}
			<button
				type="button"
				role="tab"
				aria-selected={device === tab.value}
				class="-mb-px border-b-2 px-4 py-2 text-sm font-medium focus-visible:outline-2 focus-visible:outline-primary {device ===
				tab.value
					? 'border-primary text-primary'
					: 'border-transparent text-dark/70 hover:text-dark'}"
				onclick={() => (device = tab.value)}
			>
				{tab.label}
			</button>
		{/each}
	</div>

	<div class="mt-5 grid gap-6 md:grid-cols-[minmax(0,1fr)_auto]">
		<div class="flex min-w-0 flex-col gap-4" role="tabpanel">
			{#if isLocal}
				<Callout tone="info">
					You opened this page on <code>localhost</code>, which other devices can't reach. Open the
					admin page through this server's network address (for example
					<code>http://192.168.1.10:8080/admin</code>) to get an address and code they can use.
				</Callout>
			{/if}
			<ol class="list-decimal space-y-2 pl-5 text-sm">
				{#if device === 'appletv'}
					<li>On the Apple TV, install <strong>ImmichFrame</strong> from the App Store.</li>
					<li>Open it and enter the server address{secret ? ' and secret' : ''} below.</li>
				{:else if device === 'ios'}
					<li>Scan the code with the Camera app, or open the link below in Safari.</li>
					<li>Tap Share, then <em>Add to Home Screen</em>, for a full-screen frame.</li>
					<li>Turn off Auto-Lock in Settings → Display &amp; Brightness if it stays plugged in.</li>
				{:else if device === 'android'}
					<li>
						Install <strong>ImmichFrame</strong> from Google Play. For a Frameo frame, follow the
						<a
							class="text-primary underline"
							href="https://immichframe.dev/docs/getting-started/apps#frameo"
							target="_blank"
							rel="noreferrer">Frameo guide</a
						>.
					</li>
					<li>Open it and enter the server address{secret ? ' and secret' : ''} below.</li>
				{:else}
					<li>Open the link below on that screen, or scan the code.</li>
					<li>Switch the browser to full screen and leave it running.</li>
				{/if}
				<li>Give it a name so you can tell it apart under Screens.</li>
			</ol>

			<label class="text-sm">
				<span class="mb-1 block font-medium">Screen name (optional)</span>
				<Input placeholder="Living room" bind:value={screenName} />
			</label>

			{#if usesApp}
				<CopyField label="Server address" value={origin} />
				{#if secret}<CopyField label="Secret" value={secret} secret />{/if}
			{:else}
				<CopyField label="Link" value={frameUrl} secret={!!secret} />
			{/if}

			{#if !secret}
				<Callout>
					There's no frame secret, so anyone who can reach this address can see the photos.
					<button type="button" class="font-medium underline" onclick={ongotoaccess}>
						Set a secret
					</button>
				</Callout>
			{/if}
		</div>

		<div class="flex flex-col items-center gap-2 md:w-48">
			{#if isLocal}
				<div
					class="flex aspect-square w-40 items-center justify-center rounded-2xl border border-dashed border-light-300 p-4 text-center text-sm text-dark/60"
				>
					No code on localhost
				</div>
			{:else}
				<div class="w-40 rounded-2xl border border-light-300 bg-white p-3">
					<QrCode data={frameUrl} isResponsive />
				</div>
				<Text size="small" class="max-w-48 text-center text-dark/70">
					Scan with a phone or tablet to open the frame{secret ? ', already signed in' : ''}.
				</Text>
			{/if}
		</div>
	</div>
</PageCard>
