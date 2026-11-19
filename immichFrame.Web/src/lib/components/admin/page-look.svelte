<script lang="ts">
	import PopupSelect from './popup-select.svelte';
	import Toggle from './toggle.svelte';
	import Glyph from './glyph.svelte';
	import FieldRow from './field-row.svelte';
	import * as locales from 'date-fns/locale';
	import type { GeneralSettings } from '$lib/immichFrameApi';
	import { effective, generalDefaults } from './admin-defaults';
	import { generalField } from './admin-fields';
	import PageCard from './page-card.svelte';
	import Segmented from './segmented.svelte';
	import SettingField from './setting-field.svelte';
	import SettingRow from './setting-row.svelte';

	interface Props {
		general: GeneralSettings;
	}

	let { general }: Props = $props();

	const get = <K extends keyof GeneralSettings>(key: K) => effective(general, generalDefaults, key);
	const target = $derived(general as Record<string, unknown>);

	// Timing: the slider snaps to useful steps rather than a linear 5–300 s.
	const steps = [5, 10, 15, 20, 30, 45, 60, 90, 120, 180, 300];
	let interval = $derived(get('interval') ?? 45);
	let stepIndex = $derived.by(() => {
		let best = 0;
		steps.forEach((s, i) => {
			if (Math.abs(s - interval) < Math.abs(steps[best] - interval)) best = i;
		});
		return best;
	});
	const duration = (s: number) =>
		s < 60
			? `${s} seconds`
			: s % 60 === 0
				? `${s / 60} min`
				: `${Math.floor(s / 60)} min ${s % 60} s`;
	let perHour = $derived(
		Math.round(3600 / Math.max(interval, 1)) * (get('layout') === 'splitview' ? 2 : 1)
	);
	const transitions = [
		{ value: 0.5, label: 'Quick' },
		{ value: 1, label: 'Gentle' },
		{ value: 2, label: 'Slow' }
	];
	let transitionValue = $derived(get('transitionDuration') ?? 1);
	let transitionIsPreset = $derived(transitions.some((t) => t.value === transitionValue));

	// Order: how the next photo is picked (the server's SlideshowOrder).
	const orders = [
		{ value: 'random', label: 'Random', description: 'Any photo, any time' },
		{ value: 'shuffle', label: 'Shuffle', description: 'Every photo once, then a new shuffle' },
		{ value: 'oldest', label: 'Oldest First', description: 'By date taken, from the first photo' },
		{ value: 'newest', label: 'Newest First', description: 'By date taken, from the latest photo' }
	];
	let order = $derived(orders.find((o) => o.value === get('slideshowOrder')) ?? orders[0]);

	// Calendars: how far ahead the events go (CalendarDaysAhead).
	const calendarRanges = [
		{ value: '1', label: 'Today' },
		{ value: '2', label: 'Today and Tomorrow' },
		{ value: '3', label: 'Next 3 Days' },
		{ value: '7', label: 'Next Week' },
		{ value: '14', label: 'Next 2 Weeks' },
		{ value: '31', label: 'Next Month' }
	];

	// Movement maps onto two switches.
	type Movement = 'zoom' | 'pan' | 'both' | 'still';
	let movement: Movement = $derived.by(() => {
		const zoom = get('imageZoom');
		const pan = get('imagePan');
		return zoom && pan ? 'both' : zoom ? 'zoom' : pan ? 'pan' : 'still';
	});
	function setMovement(value: Movement) {
		general.imageZoom = value === 'zoom' || value === 'both';
		general.imagePan = value === 'pan' || value === 'both';
	}

	const placeFormats = [
		{ value: 'City,State,Country', label: 'City, region, country' },
		{ value: 'City,Country', label: 'City, country' },
		{ value: 'City', label: 'City' },
		{ value: 'Country', label: 'Country' }
	];

	// Named swatches: screen readers say the name, not a hex code.
	const textColors = [
		{ value: '#f5deb3', name: 'Wheat' },
		{ value: '#ffffff', name: 'White' },
		{ value: '#1f2937', name: 'Charcoal' }
	];
	const boxColors = [
		{ value: '#000000', name: 'Black' },
		{ value: '#1f2937', name: 'Charcoal' },
		{ value: '#ffffff', name: 'White' }
	];
	const usesBox = $derived(get('style') === 'solid' || get('style') === 'transition');

	// Languages: plain codes that both date-fns and .NET understand.
	const names = new Intl.DisplayNames(['en'], { type: 'language' });
	const languageOptions = $derived.by(() => {
		const codes = Object.keys(locales).filter((code) => /^[a-z]{2,3}$/.test(code));
		const current = get('language') ?? 'en';
		if (!codes.includes(current)) codes.push(current);
		return codes
			.map((code) => {
				let label = code;
				try {
					label = names.of(code) ?? code;
				} catch {
					// keep the code
				}
				return { value: code, label };
			})
			.sort((a, b) => a.label.localeCompare(b.label));
	});

	const fontSizes = [
		{ value: '14px', label: 'Small' },
		{ value: '17px', label: 'Default' },
		{ value: '21px', label: 'Large' },
		{ value: '26px', label: 'Largest' }
	];

	let newCalendar = $state('');
	let addingCalendar = $state(false);
	function focusOnMount(node: HTMLInputElement) {
		node.focus();
	}
	function addCalendar() {
		const url = newCalendar.trim();
		if (!url) return;
		general.webcalendars = [...(general.webcalendars ?? []), url];
		newCalendar = '';
	}

	// Browsers only share location with HTTPS pages.
	const canLocate =
		typeof window !== 'undefined' && window.isSecureContext && !!navigator.geolocation;
	let locating = $state(false);
	let locateError = $state('');
	function useMyLocation() {
		locateError = '';
		if (!navigator.geolocation) {
			locateError = 'This browser can’t share its location.';
			return;
		}
		locating = true;
		navigator.geolocation.getCurrentPosition(
			(pos) => {
				locating = false;
				general.weatherLatLong = `${pos.coords.latitude.toFixed(3)},${pos.coords.longitude.toFixed(3)}`;
			},
			() => {
				locating = false;
				locateError = 'Location wasn’t shared. Type the coordinates instead.';
			},
			{ timeout: 10_000 }
		);
	}
</script>

{#snippet swatches(
	label: string,
	colors: { value: string; name: string }[],
	current: string,
	set: (v: string) => void,
	disabled = false
)}
	{@const custom = !colors.some((c) => c.value === current.toLowerCase())}
	<div class="flex items-center gap-2" role="radiogroup" aria-label={label}>
		{#each colors as color (color.value)}
			{@const selected = current.toLowerCase() === color.value}
			<button
				type="button"
				role="radio"
				aria-checked={selected}
				aria-label={color.name}
				title={color.name}
				{disabled}
				class="h-[30px] w-[30px] rounded-full shadow-[inset_0_0_0_1px_rgb(0_0_0/0.18)] transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary disabled:opacity-40 md:h-6 md:w-6 dark:shadow-[inset_0_0_0_0.5px_rgb(255_255_255/0.25)] {selected
					? 'outline-2 outline-offset-2 outline-primary'
					: ''}"
				style="background:{color.value}"
				onclick={() => set(color.value)}
			></button>
		{/each}
		<label
			class="relative h-[30px] w-[30px] cursor-pointer rounded-full shadow-[inset_0_0_0_1px_rgb(0_0_0/0.18)] md:h-6 md:w-6 dark:shadow-[inset_0_0_0_0.5px_rgb(255_255_255/0.25)] {custom
				? 'outline-2 outline-offset-2 outline-primary'
				: ''} {disabled ? 'pointer-events-none opacity-40' : ''}"
			style="background:{custom
				? current
				: 'conic-gradient(#f87171,#fbbf24,#34d399,#60a5fa,#a78bfa,#f87171)'}"
			title="Custom color"
		>
			<span class="sr-only">Custom {label.toLowerCase()}</span>
			<input
				type="color"
				class="absolute inset-0 h-full w-full cursor-pointer opacity-0"
				value={current}
				{disabled}
				oninput={(e) => set(e.currentTarget.value)}
			/>
		</label>
	</div>
{/snippet}

<div class="flex flex-col gap-6">
	<PageCard title="Slideshow">
		<SettingRow title="Order" description={order.description}>
			{#snippet control()}
				<PopupSelect
					label="Order"
					options={orders.map(({ value, label }) => ({ value, label }))}
					value={order.value}
					onchange={(v) => (general.slideshowOrder = v)}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow
			title="Time on each photo"
			description="About {perHour} photos an hour{get('layout') === 'splitview'
				? ', two at a time'
				: ''}"
		>
			{#snippet control()}
				<input
					type="range"
					min="0"
					max={steps.length - 1}
					step="1"
					value={stepIndex}
					aria-label="Time on each photo"
					aria-valuetext={duration(steps[stepIndex])}
					class="if-slider w-40 sm:w-52"
					style="--fill:{(stepIndex / (steps.length - 1)) * 100}%"
					oninput={(e) => (general.interval = steps[Number(e.currentTarget.value)])}
				/>
				<span class="text-label-2 w-20 text-right text-[0.875rem] tabular-nums"
					>{duration(interval)}</span
				>
			{/snippet}
		</SettingRow>
		<SettingRow title="Change between photos" description="How long the cross-fade takes">
			{#snippet control()}
				<Segmented
					label="Change between photos"
					value={transitionIsPreset ? transitionValue : undefined}
					options={transitions}
					onchange={(v) => (general.transitionDuration = v)}
				/>
				{#if !transitionIsPreset}<span class="text-sm tabular-nums">{transitionValue} s</span>{/if}
			{/snippet}
		</SettingRow>
		<SettingRow title="Progress bar" description="A thin line counting down to the next photo">
			{#snippet control()}
				<Toggle
					aria-label="Progress bar"
					checked={get('showProgressBar')}
					onCheckedChange={(v) => (general.showProgressBar = v)}
				/>
			{/snippet}
		</SettingRow>
	</PageCard>

	<PageCard
		title="Layout"
		footer={get('layout') === 'splitview'
			? 'Best when most photos come from phones. Landscape photos still show alone.'
			: 'Portrait photos get dark bars at the sides of a wide screen.'}
	>
		<!-- Like Appearance in System Settings: only the selected thumbnail gets a ring. -->
		<div
			class="flex justify-center gap-8 py-4 sm:justify-start sm:pl-2"
			role="radiogroup"
			aria-label="Layout"
		>
			{#each [{ value: 'single', title: 'One Photo' }, { value: 'splitview', title: 'Two Portraits' }] as option (option.value)}
				{@const selected = get('layout') === option.value}
				<button
					type="button"
					role="radio"
					aria-checked={selected}
					class="group flex flex-col items-center gap-2 focus-visible:outline-none"
					onclick={() => (general.layout = option.value)}
				>
					<span
						class="flex h-[4.5rem] w-32 items-stretch justify-center gap-1 rounded-[8px] bg-neutral-900 p-1.5 transition-shadow group-focus-visible:outline-2 group-focus-visible:outline-offset-4 group-focus-visible:outline-primary {selected
							? 'ring-[3px] ring-[var(--if-accent-fill)] ring-offset-2 ring-offset-white dark:ring-offset-[#1c1c1e]'
							: 'shadow-[0_0_0_0.5px_rgb(0_0_0/0.2)] dark:shadow-[0_0_0_0.5px_rgb(255_255_255/0.18)]'}"
					>
						{#if option.value === 'single'}
							<span class="w-[36%] rounded-[3px] bg-gradient-to-b from-sky-300/85 to-amber-200/75"
							></span>
						{:else}
							<span class="w-[36%] rounded-[3px] bg-gradient-to-b from-sky-300/85 to-amber-200/75"
							></span>
							<span class="w-[36%] rounded-[3px] bg-gradient-to-b from-emerald-300/75 to-sky-200/75"
							></span>
						{/if}
					</span>
					<span class="text-[0.75rem] {selected ? 'font-semibold' : ''}">{option.title}</span>
				</button>
			{/each}
		</div>
	</PageCard>

	<PageCard>
		<SettingRow title="Photo size" description="Fit never crops; fill uses the whole screen">
			{#snippet control()}
				<Segmented
					label="Photo size"
					value={get('imageFill') ? 'fill' : 'fit'}
					options={[
						{ value: 'fit', label: 'Fit' },
						{ value: 'fill', label: 'Fill' }
					]}
					onchange={(v) => (general.imageFill = v === 'fill')}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow title="Movement" description="A slow zoom keeps a still photo alive">
			{#snippet control()}
				<Segmented
					label="Movement"
					value={movement}
					options={[
						{ value: 'zoom', label: 'Zoom' },
						{ value: 'pan', label: 'Pan' },
						{ value: 'both', label: 'Zoom and pan' },
						{ value: 'still', label: 'Still' }
					]}
					onchange={setMovement}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow title="Video sound" description="Most screens start videos muted anyway">
			{#snippet control()}
				<Toggle
					aria-label="Video sound"
					checked={get('playAudio')}
					onCheckedChange={(v) => (general.playAudio = v)}
				/>
			{/snippet}
		</SettingRow>
	</PageCard>

	<PageCard title="On the photo" footer="Shown in the corners of every slide.">
		<SettingRow title="Clock" description="With today's date, and the weather when it's set up">
			{#snippet control()}
				<Toggle
					aria-label="Clock"
					checked={get('showClock')}
					onCheckedChange={(v) => (general.showClock = v)}
				/>
			{/snippet}
		</SettingRow>
		{#if get('showClock')}
			<SettingRow title="Time" nested>
				{#snippet control()}<SettingField
						field={generalField('clockFormat', '')}
						{target}
					/>{/snippet}
			</SettingRow>
			<SettingRow title="Date" nested>
				{#snippet control()}<SettingField
						field={generalField('clockDateFormat', '')}
						{target}
					/>{/snippet}
			</SettingRow>
		{/if}
		<SettingRow title="Date taken" description="When the photo was taken">
			{#snippet control()}
				<Toggle
					aria-label="Date taken"
					checked={get('showPhotoDate')}
					onCheckedChange={(v) => (general.showPhotoDate = v)}
				/>
			{/snippet}
		</SettingRow>
		{#if get('showPhotoDate')}
			<SettingRow title="Format" nested>
				{#snippet control()}<SettingField
						field={generalField('photoDateFormat', '')}
						{target}
					/>{/snippet}
			</SettingRow>
		{/if}
		<SettingRow title="Place" description="Phone photos usually have one">
			{#snippet control()}
				<Toggle
					aria-label="Place"
					checked={get('showImageLocation')}
					onCheckedChange={(v) => (general.showImageLocation = v)}
				/>
			{/snippet}
		</SettingRow>
		{#if get('showImageLocation')}
			<SettingRow title="Format" nested>
				{#snippet control()}<PopupSelect
						label="Place format"
						options={placeFormats.some((p) => p.value === get('imageLocationFormat'))
							? placeFormats
							: [
									...placeFormats,
									{
										value: get('imageLocationFormat') ?? '',
										label: get('imageLocationFormat') ?? ''
									}
								]}
						value={get('imageLocationFormat') ?? 'City,State,Country'}
						onchange={(v) => (general.imageLocationFormat = v)}
					/>{/snippet}
			</SettingRow>
		{/if}
		<SettingRow title="People" description="Names of the people Immich recognized">
			{#snippet control()}
				<Toggle
					aria-label="People"
					checked={get('showPeopleDesc')}
					onCheckedChange={(v) => (general.showPeopleDesc = v)}
				/>
			{/snippet}
		</SettingRow>
		{#if get('showPeopleDesc')}
			<SettingRow
				title="Ages"
				description="How old each person was, from birthdays in Immich"
				nested
			>
				{#snippet control()}
					<Toggle
						aria-label="Ages"
						checked={get('showPeopleAge')}
						onCheckedChange={(v) => (general.showPeopleAge = v)}
					/>
				{/snippet}
			</SettingRow>
		{/if}
		<SettingRow title="Album name" description="The album the photo is in">
			{#snippet control()}
				<Toggle
					aria-label="Album name"
					checked={get('showAlbumName')}
					onCheckedChange={(v) => (general.showAlbumName = v)}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow title="Tags" description="Tags added to the photo in Immich">
			{#snippet control()}
				<Toggle
					aria-label="Tags"
					checked={get('showTagsDesc')}
					onCheckedChange={(v) => (general.showTagsDesc = v)}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow title="Description" description="The photo's description in Immich">
			{#snippet control()}
				<Toggle
					aria-label="Description"
					checked={get('showImageDesc')}
					onCheckedChange={(v) => (general.showImageDesc = v)}
				/>
			{/snippet}
			{#if get('showImageDesc')}
				<p class="text-label-2 text-[0.75rem]">
					Cameras often fill it with text like “DJI_0073.JPG”.
				</p>
			{/if}
		</SettingRow>
	</PageCard>

	<PageCard title="Text and colors">
		<SettingRow
			title="Behind the clock and captions"
			description="Keeps text readable on bright photos"
		>
			{#snippet control()}
				<Segmented
					label="Behind the clock and captions"
					value={get('style') ?? 'none'}
					options={[
						{ value: 'blur', label: 'Frosted' },
						{ value: 'solid', label: 'Solid' },
						{ value: 'transition', label: 'Fade' },
						{ value: 'none', label: 'None' }
					]}
					onchange={(v) => (general.style = String(v))}
				/>
			{/snippet}
		</SettingRow>

		<SettingRow title="Text color" description="The clock and the captions">
			{#snippet control()}
				{@render swatches(
					'Text color',
					textColors,
					general.primaryColor || '#f5deb3',
					(v) => (general.primaryColor = v)
				)}
			{/snippet}
		</SettingRow>
		{#if usesBox}
			<SettingRow title="Box color" description="The box behind the clock and captions">
				{#snippet control()}
					{@render swatches(
						'Box color',
						boxColors,
						general.secondaryColor || '#000000',
						(v) => (general.secondaryColor = v),
						!usesBox
					)}
				{/snippet}
			</SettingRow>
		{/if}
		<SettingRow title="Text size">
			{#snippet control()}
				<Segmented
					label="Text size"
					value={general.baseFontSize || '17px'}
					options={fontSizes}
					onchange={(v) => (general.baseFontSize = v === '17px' ? null : v)}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow title="Language" description="For dates, the clock and the weather">
			{#snippet control()}
				<PopupSelect
					label="Language"
					options={languageOptions}
					value={get('language') ?? 'en'}
					onchange={(v) => (general.language = v)}
				/>
			{/snippet}
		</SettingRow>
	</PageCard>

	<PageCard
		title="Weather"
		footer={general.weatherApiKey
			? 'Shown under the clock.'
			: 'Shown under the clock. It needs a free OpenWeatherMap API key; without one, the weather is hidden.'}
	>
		<FieldRow
			title="API key"
			type="password"
			placeholder="Required"
			value={general.weatherApiKey}
			oninput={(v) => (general.weatherApiKey = v)}
		/>
		<FieldRow
			title="Location"
			placeholder="Latitude, longitude"
			value={general.weatherLatLong}
			oninput={(v) => (general.weatherLatLong = v)}
		/>
		{#if canLocate}
			<button
				type="button"
				class="border-sep flex min-h-11 w-full items-center border-t py-2 text-left text-[0.9375rem] text-primary disabled:opacity-50 md:text-[0.875rem]"
				disabled={locating}
				onclick={useMyLocation}
			>
				{locating ? 'Finding Your Location…' : 'Use Current Location'}
			</button>
		{/if}
		{#if locateError}<p class="pb-2 text-[0.75rem] text-[#d70015] dark:text-[#ff6961]">
				{locateError}
			</p>{/if}
		{#if general.weatherApiKey}
			<SettingRow title="Units">
				{#snippet control()}
					<Segmented
						label="Units"
						value={get('unitSystem') ?? 'imperial'}
						options={[
							{ value: 'metric', label: '°C' },
							{ value: 'imperial', label: '°F' }
						]}
						onchange={(v) => (general.unitSystem = String(v))}
					/>
				{/snippet}
			</SettingRow>
			<SettingRow title="Weather description" description="e.g. “light rain” under the temperature">
				{#snippet control()}
					<Toggle
						aria-label="Weather description"
						checked={get('showWeatherDescription')}
						onCheckedChange={(v) => (general.showWeatherDescription = v)}
					/>
				{/snippet}
			</SettingRow>
		{/if}
	</PageCard>

	<PageCard
		title="Calendars"
		footer="Upcoming events in the top corner. Paste a calendar's .ics link; changes show within 15 minutes."
	>
		{#if (general.webcalendars ?? []).length}
			<SettingRow title="Show events for">
				{#snippet control()}
					<PopupSelect
						label="Show events for"
						options={calendarRanges}
						value={String(get('calendarDaysAhead') ?? 1)}
						onchange={(v) => (general.calendarDaysAhead = Number(v))}
					/>
				{/snippet}
			</SettingRow>
		{/if}
		<div data-setting="Calendars">
			{#each general.webcalendars ?? [] as url, i (i)}
				<div class="border-sep flex min-h-11 items-center gap-3 border-t py-2 first:border-t-0">
					<span class="min-w-0 flex-1 truncate text-[0.875rem]">{url}</span>
					<button
						type="button"
						class="text-label-3 -mr-1 rounded-full p-1 hover:text-[#d70015] focus-visible:outline-2 focus-visible:outline-primary dark:hover:text-[#ff6961]"
						aria-label="Remove calendar"
						onclick={() =>
							(general.webcalendars = (general.webcalendars ?? []).filter((_, j) => j !== i))}
					>
						<Glyph name="circleMinus" size={18} />
					</button>
				</div>
			{/each}
			{#if addingCalendar}
				<form
					class="border-sep flex min-h-11 items-center gap-3 border-t py-1.5 first:border-t-0"
					onsubmit={(e) => {
						e.preventDefault();
						addCalendar();
						addingCalendar = false;
					}}
				>
					<input
						use:focusOnMount
						bind:value={newCalendar}
						placeholder="https://… .ics"
						aria-label="Calendar link"
						class="min-w-0 flex-1 bg-transparent py-2 text-[0.9375rem] outline-none placeholder:text-[#8e8e93] md:text-[0.875rem]"
						onkeydown={(e) => e.key === 'Escape' && (addingCalendar = false)}
					/>
					<button
						type="button"
						class="text-label-2 text-[0.875rem]"
						onclick={() => (addingCalendar = false)}>Cancel</button
					>
					<button
						type="submit"
						class="text-[0.875rem] font-semibold text-primary disabled:opacity-40"
						disabled={!newCalendar.trim()}>Add</button
					>
				</form>
			{:else}
				<button
					type="button"
					class="border-sep flex min-h-11 w-full items-center border-t py-2 text-left text-[0.9375rem] text-primary first:border-t-0 md:text-[0.875rem]"
					onclick={() => (addingCalendar = true)}>Add Calendar…</button
				>
			{/if}
		</div>
	</PageCard>
</div>
