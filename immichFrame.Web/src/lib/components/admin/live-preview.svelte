<script lang="ts">
	import { onMount } from 'svelte';
	import { Card, CardBody, Text } from '@immich/ui';
	import type { GeneralSettings } from '$lib/immichFrameApi';
	import { effective, generalDefaults } from './admin-defaults';
	import { formatExample } from './format-presets';
	import type { AdminPreview } from './admin-preview.svelte';

	interface Props {
		general: GeneralSettings;
		preview: AdminPreview;
	}

	let { general, preview }: Props = $props();

	const get = <K extends keyof GeneralSettings>(key: K) => effective(general, generalDefaults, key);

	let now = $state(new Date());
	onMount(() => {
		const timer = setInterval(() => (now = new Date()), 15_000);
		return () => clearInterval(timer);
	});

	const lang = $derived(get('language'));
	const clock = $derived(formatExample(get('clockFormat') ?? 'hh:mm', lang, false, now) ?? '');
	const clockDate = $derived(
		formatExample(get('clockDateFormat') ?? 'eee, MMM d', lang, true, now) ?? ''
	);
	const split = $derived(get('layout') === 'splitview');
	const photos = $derived(preview.items.slice(0, split ? 2 : 1));
	const first = $derived(photos[0]);
	const taken = $derived(
		first?.takenAt
			? (formatExample(
					get('photoDateFormat') ?? 'MM/dd/yyyy',
					lang,
					true,
					new Date(first.takenAt)
				) ?? '')
			: ''
	);
	const place = $derived(
		(get('imageLocationFormat') ?? 'City,State,Country')
			.split(',')
			.map((part) =>
				part === 'City'
					? first?.city
					: part === 'Country'
						? first?.country
						: part === 'State'
							? null
							: part
			)
			.filter(Boolean)
			.join(', ')
	);
	const people = $derived((first?.people ?? []).join(', '));

	const text = $derived(general.primaryColor || '#f5deb3');
	const box = $derived(general.secondaryColor || '#000000');
	const style = $derived(get('style'));
	const scale = $derived(parseFloat(general.baseFontSize || '17px') / 17);

	function boxStyle(corner: 'left' | 'right') {
		if (style === 'solid') return `background:${box};`;
		if (style === 'transition')
			return `background:linear-gradient(to ${corner === 'left' ? 'right' : 'left'}, ${box}, transparent);`;
		if (style === 'blur') return 'backdrop-filter:blur(12px);background:rgba(255,255,255,.08);';
		return '';
	}
	const fit = $derived(get('imageFill') ? 'object-cover' : 'object-contain');
</script>

<Card>
	<CardBody>
		<Text fontWeight="semi-bold" class="mb-3">Live preview</Text>
		<div
			class="relative aspect-video overflow-hidden rounded-xl bg-black"
			style="color:{text}; font-size:{scale}rem"
			aria-label="Preview of the frame"
		>
			<div class="absolute inset-0 flex gap-0.5">
				{#if photos.length}
					{#each photos as photo (photo.id)}
						<div class="relative h-full flex-1 overflow-hidden">
							{#if photo.url}
								<img src={photo.url} alt="" class="h-full w-full {fit}" />
							{/if}
						</div>
					{/each}
				{:else}
					<div class="h-full flex-1 bg-gradient-to-br from-slate-500 to-slate-700"></div>
					{#if split}<div
							class="h-full flex-1 bg-gradient-to-br from-slate-400 to-slate-600"
						></div>{/if}
				{/if}
			</div>

			{#if get('showClock')}
				<div class="absolute bottom-0 left-0 rounded-tr-xl px-3 py-1.5" style={boxStyle('left')}>
					<div class="text-[1.6em] leading-none font-bold [text-shadow:0_1px_3px_rgba(0,0,0,.6)]">
						{clock}
					</div>
					<div class="text-[0.6em] [text-shadow:0_1px_2px_rgba(0,0,0,.6)]">{clockDate}</div>
				</div>
			{/if}

			{#if (get('showPhotoDate') && taken) || (get('showImageLocation') && place) || (get('showPeopleDesc') && people)}
				<div
					class="absolute right-0 bottom-0 max-w-[60%] rounded-tl-xl px-3 py-1.5 text-right text-[0.6em] leading-snug [text-shadow:0_1px_2px_rgba(0,0,0,.6)]"
					style={boxStyle('right')}
				>
					{#if get('showPeopleDesc') && people}<div class="truncate">{people}</div>{/if}
					<div class="truncate">
						{[get('showImageLocation') ? place : '', get('showPhotoDate') ? taken : '']
							.filter(Boolean)
							.join(' · ')}
					</div>
				</div>
			{/if}

			{#if get('showProgressBar')}
				<div class="absolute top-0 left-0 h-0.5 w-1/3" style="background:{text}"></div>
			{/if}
		</div>
		<Text size="small" color="muted" class="mt-3">
			{split
				? 'Two portraits share the screen; a landscape photo fills it alone.'
				: 'One photo at a time.'}
			Photos come from the saved settings; the look follows your unsaved changes.
		</Text>
	</CardBody>
</Card>
