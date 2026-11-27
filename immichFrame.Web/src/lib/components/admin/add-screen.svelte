<script lang="ts">
	import QrCode from '@castlenine/svelte-qrcode';
	import FieldRow from './field-row.svelte';
	import ValueRow from './value-row.svelte';
	import PageCard from './page-card.svelte';
	import Segmented from './segmented.svelte';

	interface Props {
		/** The saved frame secret (screens use the saved one, not an unsaved edit). */
		secret: string | null | undefined;
		ongotoaccess: () => void;
	}

	let { secret, ongotoaccess }: Props = $props();

	type Device = 'appletv' | 'ios' | 'android' | 'browser';
	let device = $state<Device>('appletv');
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
	// The name's example fits the device being added (only the web devices have a Name field).
	let nameExample = $derived(device === 'ios' ? 'Kitchen iPad' : 'Hallway Display');
	let note = $derived(
		isLocal
			? 'This page is open on localhost, so the address only works on this computer. Open the admin page using this server’s IP address and port to get an address and code other devices can use.'
			: !secret
				? 'There’s no frame secret, so anyone who can reach this address can see the photos.'
				: usesApp
					? 'The app lists itself in Screens once it shows its first photo.'
					: 'Scan the code with a phone or tablet to open the frame, already signed in.'
	);
</script>

{#snippet enterAddress()}
	Open it and enter the server address{secret && !isLocal ? ' and secret' : ''} below{#if secret && isLocal},
		and the frame secret from <button
			type="button"
			class="text-primary underline-offset-2 hover:underline"
			onclick={ongotoaccess}>Access &amp; Accounts</button
		>{/if}.
{/snippet}

<PageCard title="Add a screen" footer={note}>
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
		{#if !usesApp}
			<!-- First: the name goes into the link and the code, so it has to come before scanning. -->
			<div class="mt-3 -mb-1">
				<FieldRow
					title="Name"
					placeholder={`Optional, e.g. ${nameExample}`}
					value={screenName}
					oninput={(v) => (screenName = v)}
				/>
			</div>
		{/if}
		<div class="mt-4 flex gap-5">
			<ol
				class="min-w-0 flex-1 list-inside list-decimal space-y-0.5 text-[0.8125rem] leading-[1.45]"
			>
				{#if device === 'appletv'}
					<li>On the Apple TV, install <strong>ImmichFrame</strong> from the App Store.</li>
					<li>{@render enterAddress()}</li>
				{:else if device === 'ios'}
					<li>Name it above, then scan the code with the Camera app or open the link in Safari.</li>
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
					<li>{@render enterAddress()}</li>
				{:else}
					<li>Name it above, then open the link on that screen or scan the code.</li>
					<li>Switch the browser to full screen and leave it running.</li>
				{/if}
			</ol>
			<!-- The code opens the frame in a browser: no use to the TV and Android apps. -->
			{#if !isLocal && !usesApp}
				<div class="hidden w-28 shrink-0 sm:block">
					<div class="rounded-[8px] bg-white p-2 shadow-[0_0_0_0.5px_rgb(0_0_0/0.15)]">
						<QrCode data={frameUrl} isResponsive />
					</div>
				</div>
			{/if}
		</div>
	</div>
	{#if usesApp}
		<ValueRow title="Server address" value={origin} copyable={!isLocal} />
		{#if secret && !isLocal}<ValueRow title="Secret" value={secret} secret />{/if}
	{:else}
		<ValueRow title="Link" value={frameUrl} secret={!!secret} copyable={!isLocal} />
	{/if}
	{#if !isLocal && !usesApp}
		<div class="border-sep flex justify-center border-t py-4 sm:hidden">
			<div class="w-40 rounded-[8px] bg-white p-2 shadow-[0_0_0_0.5px_rgb(0_0_0/0.15)]">
				<QrCode data={frameUrl} isResponsive />
			</div>
		</div>
	{/if}
	{#if !secret && !isLocal}
		<button
			type="button"
			class="border-sep flex min-h-11 w-full items-center border-t py-2 text-left text-[0.9375rem] text-primary md:text-[0.875rem]"
			onclick={ongotoaccess}>Set a Frame Secret…</button
		>
	{/if}
</PageCard>
