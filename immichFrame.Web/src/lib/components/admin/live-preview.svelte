<script lang="ts">
	import Btn from './btn.svelte';
	import { onMount } from 'svelte';
	import PageCard from './page-card.svelte';
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

	// The frame is drawn on a 1920×1080 stage with the slideshow's own sizes, then scaled to
	// fit, so text sizes look as they will on a TV.
	const STAGE_W = 1920;
	const STAGE_H = 1080;
	let width = $state(360);
	let scale = $derived(width / STAGE_W);
	let expanded = $state(false);
	let bigWidth = $state(1280);
	$effect(() => {
		if (!expanded) return;
		// After the portal has moved the overlay (moving an element drops its focus).
		const frame = window.setTimeout(() => document.getElementById('preview-close')?.focus(), 50);
		const onKey = (e: KeyboardEvent) => e.key === 'Escape' && (expanded = false);
		window.addEventListener('keydown', onKey);
		return () => {
			clearTimeout(frame);
			window.removeEventListener('keydown', onKey);
		};
	});

	// The side column is sticky, which traps fixed children under the header; render the
	// large preview at the end of <body> instead.
	function portal(node: HTMLElement) {
		document.body.appendChild(node);
		return { destroy: () => node.remove() };
	}

	let now = $state(new Date());
	let slide = $state(0);
	onMount(() => {
		const clock = setInterval(() => (now = new Date()), 15_000);
		return () => clearInterval(clock);
	});

	// Cycle the sample every few seconds, with the configured cross-fade.
	const reducedMotion =
		typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
	$effect(() => {
		if (reducedMotion || preview.items.length < 2) return;
		const timer = setInterval(() => (slide = slide + 1), 5000);
		return () => clearInterval(timer);
	});

	const lang = $derived(get('language'));
	const split = $derived(get('layout') === 'splitview');
	const perSlide = $derived(split ? 2 : 1);
	const slides = $derived.by(() => {
		const items = preview.items;
		if (!items.length) return [];
		const groups = [];
		for (let i = 0; i + perSlide <= items.length || (i === 0 && items.length); i += perSlide) {
			groups.push(items.slice(i, i + perSlide));
		}
		return groups;
	});
	const active = $derived(slides.length ? slide % slides.length : 0);
	const first = $derived(slides[active]?.[0]);

	const clock = $derived(formatExample(get('clockFormat') ?? 'hh:mm', lang, false, now) ?? '');
	const clockDate = $derived(
		formatExample(get('clockDateFormat') ?? 'eee, MMM d', lang, true, now) ?? ''
	);
	const taken = $derived(
		formatExample(
			get('photoDateFormat') ?? 'MM/dd/yyyy',
			lang,
			true,
			first?.takenAt ? new Date(first.takenAt) : new Date(2025, 7, 12)
		) ?? ''
	);
	const place = $derived(
		(get('imageLocationFormat') ?? 'City,State,Country')
			.split(',')
			.map((part) =>
				part === 'City'
					? (first?.city ?? 'Lisbon')
					: part === 'Country'
						? (first?.country ?? 'Portugal')
						: part === 'State'
							? null
							: part
			)
			.filter(Boolean)
			.join(', ')
	);
	const people = $derived(first ? (first.people ?? []).join(', ') : 'Mia, Leo');

	const captions = $derived(
		[
			get('showPhotoDate') && taken,
			get('showImageDesc') && 'Beach day',
			get('showAlbumName') && 'Summer 2025',
			get('showPeopleDesc') && people,
			get('showTagsDesc') && 'Family',
			get('showImageLocation') && place
		].filter((x): x is string => !!x)
	);

	const weather = $derived(!!general.weatherApiKey);
	const calendars = $derived((general.webcalendars ?? []).length > 0);

	const text = $derived(general.primaryColor || '#f5deb3');
	const box = $derived(general.secondaryColor || '#000000');
	const style = $derived(get('style'));
	const base = $derived(parseFloat(general.baseFontSize || '') || 16);
	const fit = $derived(get('imageFill') ? 'object-cover' : 'object-contain');
	const zoom = $derived(!!get('imageZoom'));
	const pan = $derived(!!get('imagePan'));
	const motion = $derived(
		reducedMotion ? '' : zoom && pan ? 'kb-both' : zoom ? 'kb-zoom' : pan ? 'kb-pan' : ''
	);
	const fade = $derived(get('transitionDuration') ?? 1);

	function boxStyle(side: 'left' | 'right') {
		const corner = side === 'left' ? 'border-top-right-radius' : 'border-top-left-radius';
		if (style === 'solid') return `background:${box};${corner}:16px;`;
		if (style === 'transition')
			return `background:linear-gradient(to ${side === 'left' ? 'right' : 'left'}, ${box}, transparent);padding-${side === 'left' ? 'right' : 'left'}:40px;`;
		if (style === 'blur')
			return `backdrop-filter:blur(24px);-webkit-backdrop-filter:blur(24px);${corner}:16px;`;
		return '';
	}

	const summary = $derived(
		[
			split
				? 'Two portraits share the screen; a landscape photo fills it alone.'
				: 'One photo at a time.',
			get('showClock') ? null : 'Clock off.'
		]
			.filter(Boolean)
			.join(' ')
	);
</script>

<PageCard
	title="Preview"
	footer="Shows your changes before you save. The photos are a sample of what's saved."
>
	{#snippet actions()}
		<Btn
			glyph="maximize2"
			variant="ghost"
			color="primary"
			size="small"
			aria-label="Show the preview larger"
			onclick={() => (expanded = true)}
		/>
	{/snippet}
	<div class="py-4">
		<button
			type="button"
			class="relative block w-full cursor-zoom-in overflow-hidden rounded-[10px] bg-black shadow-[0_0_0_0.5px_rgb(0_0_0/0.15)]"
			style="aspect-ratio:16/9"
			bind:clientWidth={width}
			aria-label="Preview of the frame: {summary} Show larger."
			onclick={() => (expanded = true)}
		>
			{@render stage(scale)}
		</button>
	</div>
</PageCard>

{#if expanded}
	<div
		use:portal
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/75 p-4 backdrop-blur-sm"
		role="dialog"
		aria-modal="true"
		aria-label="Larger preview"
		tabindex="-1"
		onkeydown={(e) => e.key === 'Escape' && (expanded = false)}
		onclick={(e) => e.target === e.currentTarget && (expanded = false)}
	>
		<div class="w-[min(92vw,1280px)]">
			<div
				class="relative overflow-hidden rounded-xl bg-black shadow-2xl"
				style="aspect-ratio:16/9"
				bind:clientWidth={bigWidth}
			>
				{@render stage(bigWidth / STAGE_W)}
			</div>
			<div class="mt-3 flex items-center justify-between gap-3 text-sm text-white/80">
				<span>{summary}</span>
				<Btn size="small" color="secondary" id="preview-close" onclick={() => (expanded = false)}
					>Close</Btn
				>
			</div>
		</div>
	</div>
{/if}

{#snippet stage(stageScale: number)}
	<div
		class="absolute top-0 left-0 origin-top-left"
		style="width:{STAGE_W}px;height:{STAGE_H}px;transform:scale({stageScale});color:{text};font-size:{base}px"
	>
		<!-- Photos, cross-fading between sample slides -->
		{#if slides.length}
			{#each slides as group, i (i)}
				<div
					class="absolute inset-0 flex gap-[6px] transition-opacity ease-in-out"
					style="opacity:{i === active ? 1 : 0};transition-duration:{fade}s"
				>
					{#each group as photo (photo.id)}
						<div class="relative h-full flex-1 overflow-hidden">
							{#if photo.url}
								<img
									src={photo.url}
									alt=""
									class="h-full w-full {fit} {i === active ? motion : ''}"
								/>
							{/if}
						</div>
					{/each}
				</div>
			{/each}
		{:else}
			<div class="absolute inset-0 flex gap-[6px]">
				<div class="h-full flex-1 bg-gradient-to-br from-slate-500 to-slate-700 {motion}"></div>
				{#if split}<div
						class="h-full flex-1 bg-gradient-to-br from-slate-400 to-slate-600"
					></div>{/if}
			</div>
		{/if}

		{#if get('showProgressBar')}
			<div class="absolute top-0 left-0 h-[6px] w-1/3" style="background:{text}"></div>
		{/if}

		{#if calendars}
			<div
				class="absolute top-0 right-0 m-5 text-center"
				style="text-shadow:2px 2px 4px rgb(0 0 0/.8)"
			>
				<div class="font-semibold" style="font-size:1.25em">Today</div>
				<div style="font-size:1em">14:00&nbsp;·&ensp;Swimming</div>
				<div style="font-size:1em">18:30&nbsp;·&ensp;Dinner at grandma's</div>
			</div>
		{/if}

		{#if get('showClock')}
			<div class="absolute bottom-0 left-0 p-3 text-center" style={boxStyle('left')}>
				<p class="mt-2 font-thin" style="font-size:1.25em;text-shadow:2px 2px 4px rgb(0 0 0/.8)">
					{clockDate}
				</p>
				<p
					class="mt-2 leading-none font-bold"
					style="font-size:6em;text-shadow:3px 3px 6px rgb(0 0 0/.5)"
				>
					{clock}
				</p>
				{#if weather}
					<p
						class="mt-2 font-semibold"
						style="font-size:1.875em;text-shadow:2px 2px 4px rgb(0 0 0/.8)"
					>
						Lisbon, 18.5°
					</p>
					{#if get('showWeatherDescription')}
						<p style="font-size:1.25em;text-shadow:2px 2px 4px rgb(0 0 0/.8)">light rain</p>
					{/if}
				{/if}
			</div>
		{/if}

		{#if captions.length}
			<div
				class="absolute right-0 bottom-0 max-w-[45%] p-1 text-right"
				style="{boxStyle('right')};text-shadow:2px 2px 4px rgb(0 0 0/.8)"
			>
				{#each captions as line (line)}
					<p class="truncate px-2 py-0.5" style="font-size:1em">{line}</p>
				{/each}
			</div>
		{/if}
	</div>
{/snippet}

<style>
	/* A few seconds of the slideshow's movement, looping. */
	:global(.kb-zoom) {
		animation: kb-zoom 6s ease-in-out infinite alternate;
	}
	:global(.kb-pan) {
		animation: kb-pan 6s ease-in-out infinite alternate;
	}
	:global(.kb-both) {
		animation: kb-both 6s ease-in-out infinite alternate;
	}
	@keyframes kb-zoom {
		from {
			transform: scale(1);
		}
		to {
			transform: scale(1.12);
		}
	}
	@keyframes kb-pan {
		from {
			transform: scale(1.1) translateX(-3%);
		}
		to {
			transform: scale(1.1) translateX(3%);
		}
	}
	@keyframes kb-both {
		from {
			transform: scale(1) translateX(-2%);
		}
		to {
			transform: scale(1.14) translateX(2%);
		}
	}
</style>
