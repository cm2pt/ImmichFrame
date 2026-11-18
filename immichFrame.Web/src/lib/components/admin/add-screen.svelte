<script lang="ts">
	import QrCode from '@castlenine/svelte-qrcode';
	import { Input, Text } from '@immich/ui';
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
	let frameUrl = $derived.by(() => {
		// Rebuilt on every change, so a plain (non-reactive) instance is right here.
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const params = new URLSearchParams();
		if (screenName.trim()) params.set('client', screenName.trim());
		if (secret) params.set('authsecret', secret);
		const query = params.toString();
		return `${origin}/${query ? '?' + query : ''}`;
	});

	const tabs: { value: Device; label: string }[] = [
		{ value: 'appletv', label: 'Apple TV' },
		{ value: 'ios', label: 'iPad / iPhone' },
		{ value: 'android', label: 'Android / Frameo' },
		{ value: 'browser', label: 'Any browser' }
	];
</script>

<PageCard
	title="Add a screen"
	description="Pick the device; the steps and the link fill themselves in."
>
	<div
		role="tablist"
		aria-label="Device"
		class="mt-2 flex flex-wrap gap-1 border-b border-light-200"
	>
		{#each tabs as tab (tab.value)}
			<button
				type="button"
				role="tab"
				aria-selected={device === tab.value}
				class="-mb-px border-b-2 px-4 py-2 text-sm font-medium {device === tab.value
					? 'border-primary text-primary'
					: 'border-transparent text-muted hover:text-dark'}"
				onclick={() => (device = tab.value)}
			>
				{tab.label}
			</button>
		{/each}
	</div>

	<div class="mt-4 grid gap-6 md:grid-cols-[1fr_auto]">
		<div class="flex flex-col gap-4" role="tabpanel">
			<ol class="list-decimal space-y-2 pl-5 text-sm">
				{#if device === 'appletv'}
					<li>On the Apple TV, install <strong>ImmichFrame</strong> from the App Store.</li>
					<li>Open it and enter the server address{secret ? ' and the secret' : ''} below.</li>
				{:else if device === 'ios'}
					<li>
						Scan the code with the camera, or install <strong>ImmichFrame</strong> from the App
						Store and enter the address{secret ? ' and secret' : ''} below.
					</li>
					<li>In Safari: Share → <em>Add to Home Screen</em> for a full-screen frame.</li>
					<li>Turn off Auto-Lock (Settings → Display) on a frame that stays plugged in.</li>
				{:else if device === 'android'}
					<li>
						Install <strong>ImmichFrame</strong> from Google Play (Frameo frames: see the
						<a
							class="text-primary underline"
							href="https://immichframe.dev/docs/getting-started/apps#frameo"
							target="_blank"
							rel="noreferrer">Frameo guide</a
						>).
					</li>
					<li>Enter the server address{secret ? ' and the secret' : ''} below.</li>
				{:else}
					<li>Open the link below in the browser on that screen, or scan the code.</li>
					<li>Press F11 (or the browser's full-screen mode) and leave it running.</li>
				{/if}
				<li>Give it a name so you can tell it apart under Screens.</li>
			</ol>

			<label class="text-sm">
				<span class="mb-1 block font-medium">Screen name (optional)</span>
				<Input placeholder="Living room" bind:value={screenName} />
			</label>

			{#if device === 'appletv' || device === 'android'}
				<CopyField label="Server address" value={origin} />
				{#if secret}<CopyField label="Secret" value={secret} secret />{/if}
			{:else}
				<CopyField label="Link" value={frameUrl} secret={!!secret} />
			{/if}

			{#if !secret}
				<Text size="small" color="warning">
					There's no frame secret, so anyone who can reach this address sees the photos.
					<button type="button" class="underline" onclick={ongotoaccess}>Set one</button>.
				</Text>
			{/if}
		</div>

		<div class="flex flex-col items-center gap-2 md:w-52">
			<div class="w-44 rounded-2xl border border-light-300 bg-white p-3">
				<QrCode data={frameUrl} isResponsive />
			</div>
			<Text size="small" color="muted" class="text-center">
				Scan to open the frame on a phone or tablet{secret ? ', already signed in' : ''}.
			</Text>
		</div>
	</div>
</PageCard>
