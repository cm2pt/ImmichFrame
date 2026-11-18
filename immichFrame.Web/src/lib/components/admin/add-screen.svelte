<script lang="ts">
	import QrCode from '@castlenine/svelte-qrcode';
	import { Input } from '@immich/ui';
	import CopyField from './copy-field.svelte';
	import PageCard from './page-card.svelte';
	import Segmented from './segmented.svelte';

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

	let usesApp = $derived(device === 'appletv' || device === 'android');
	let note = $derived(
		isLocal
			? 'This page is open on localhost, which other devices can’t reach. Open the admin page using this server’s IP address and port to get an address and code they can use.'
			: !secret
				? 'There’s no frame secret, so anyone who can reach this address can see the photos.'
				: undefined
	);
</script>

<PageCard title="Add a Screen">
	<div class="py-3">
		<Segmented
			label="Device"
			value={device}
			options={[
				{ value: 'appletv', label: 'Apple TV' },
				{ value: 'ios', label: 'iPad or iPhone' },
				{ value: 'android', label: 'Android' },
				{ value: 'browser', label: 'Browser' }
			]}
			onchange={(v) => (device = v)}
		/>
	</div>

	<div
		class="border-sep grid gap-6 border-t py-4 {isLocal ? '' : 'md:grid-cols-[minmax(0,1fr)_auto]'}"
	>
		<div class="flex min-w-0 flex-col gap-4">
			<ol class="list-decimal space-y-1.5 pl-5 text-[0.875rem] leading-snug">
				{#if device === 'appletv'}
					<li>On the Apple TV, install <strong>ImmichFrame</strong> from the App Store.</li>
					<li>Open it and enter the server address{secret ? ' and secret' : ''} below.</li>
				{:else if device === 'ios'}
					<li>Scan the code with the Camera app, or open the link in Safari.</li>
					<li>Tap Share, then <em>Add to Home Screen</em>, for a full-screen frame.</li>
					<li>Turn off Auto-Lock in Settings → Display &amp; Brightness if it stays plugged in.</li>
				{:else if device === 'android'}
					<li>
						Install <strong>ImmichFrame</strong> from Google Play. For a Frameo frame, follow the
						<a
							class="text-primary"
							href="https://immichframe.dev/docs/getting-started/apps#frameo"
							target="_blank"
							rel="noreferrer">Frameo guide</a
						>.
					</li>
					<li>Open it and enter the server address{secret ? ' and secret' : ''} below.</li>
				{:else}
					<li>Open the link on that screen, or scan the code.</li>
					<li>Switch the browser to full screen and leave it running.</li>
				{/if}
				<li>Give it a name so you can tell it apart above.</li>
			</ol>

			<label class="text-[0.8125rem]">
				<span class="mb-1 block font-medium">Screen Name (optional)</span>
				<Input placeholder="Living room" bind:value={screenName} />
			</label>

			{#if isLocal}
				<div>
					<p class="mb-1 text-[0.8125rem] font-medium">{usesApp ? 'Server Address' : 'Link'}</p>
					<p class="bg-fill rounded-control text-label-2 px-3 py-2 text-[0.875rem] break-all">
						{origin}
					</p>
					<p class="text-label-2 mt-1 text-[0.75rem]">Only works on this computer.</p>
				</div>
			{:else if usesApp}
				<CopyField label="Server Address" value={origin} />
				{#if secret}<CopyField label="Secret" value={secret} secret />{/if}
			{:else}
				<CopyField label="Link" value={frameUrl} secret={!!secret} />
			{/if}
		</div>

		{#if !isLocal}
			<div class="flex flex-col items-center gap-2 md:w-44">
				<div class="w-36 rounded-[10px] bg-white p-2.5 shadow-[0_0_0_0.5px_rgb(0_0_0/0.15)]">
					<QrCode data={frameUrl} isResponsive />
				</div>
				<p class="text-label-2 text-center text-[0.75rem]">
					Scan with a phone or tablet to open the frame{secret ? ', already signed in' : ''}.
				</p>
			</div>
		{/if}
	</div>

	{#snippet after()}
		{#if note}
			<p class="text-label-2 mt-1.5 px-4 text-[0.8125rem] leading-snug">
				{note}
				{#if !secret && !isLocal}
					<button type="button" class="text-primary" onclick={ongotoaccess}>Set a Secret…</button>
				{/if}
			</p>
		{/if}
	{/snippet}
</PageCard>
