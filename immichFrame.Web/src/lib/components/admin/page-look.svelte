<script lang="ts">
	import { mdiAlertOutline, mdiDelete, mdiPlus } from '@mdi/js';
	import { Button, Icon, IconButton, Input, PasswordInput, Select, Switch, Text } from '@immich/ui';
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

	// Timing
	let interval = $derived(get('interval') ?? 45);
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

	// Place
	const placeFormats = [
		{ value: 'City,State,Country', label: 'City, region, country' },
		{ value: 'City,Country', label: 'City, country' },
		{ value: 'City', label: 'City' },
		{ value: 'Country', label: 'Country' }
	];

	// Colours: the text colour, and the box behind it.
	const textColours = ['#f5deb3', '#ffffff', '#1f2937'];
	const boxColours = ['#000000', '#1f2937', '#ffffff'];

	// Languages: plain language codes that both date-fns and .NET understand.
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
				return { value: code, label: `${label} (${code})` };
			})
			.sort((a, b) => a.label.localeCompare(b.label));
	});

	const fontSizes = [
		{ value: '14px', label: 'Small' },
		{ value: '17px', label: 'Normal' },
		{ value: '21px', label: 'Large' },
		{ value: '26px', label: 'Extra large' }
	];

	// Calendars
	let newCalendar = $state('');
	function addCalendar() {
		const url = newCalendar.trim();
		if (!url) return;
		general.webcalendars = [...(general.webcalendars ?? []), url];
		newCalendar = '';
	}
</script>

<div class="flex flex-col gap-6">
	<PageCard title="Timing" id="timing">
		<SettingRow
			title="Time on each photo"
			description="About {perHour} photos an hour{get('layout') === 'splitview'
				? ' (two per slide)'
				: ''}"
		>
			{#snippet control()}
				<input
					type="range"
					min="5"
					max="300"
					step="5"
					value={interval}
					aria-label="Seconds on each photo"
					class="w-48 accent-primary sm:w-64"
					oninput={(e) => (general.interval = Number(e.currentTarget.value))}
				/>
				<span class="w-14 text-right font-mono">{interval} s</span>
			{/snippet}
		</SettingRow>
		<SettingRow title="Change between photos" description="A cross-fade between slides">
			{#snippet control()}
				<Segmented
					label="Change between photos"
					value={transitionIsPreset ? transitionValue : undefined}
					options={transitions}
					onchange={(v) => (general.transitionDuration = v)}
				/>
				{#if !transitionIsPreset}<span class="font-mono text-sm">{transitionValue} s</span>{/if}
			{/snippet}
		</SettingRow>
		<SettingRow title="Progress bar" description="A thin line counting down to the next photo">
			{#snippet control()}
				<Switch
					aria-label="Progress bar"
					checked={get('showProgressBar')}
					onCheckedChange={(v) => (general.showProgressBar = v)}
				/>
			{/snippet}
		</SettingRow>
	</PageCard>

	<PageCard title="Layout" id="layout">
		<div class="grid gap-4 py-2 sm:grid-cols-2" role="radiogroup" aria-label="Layout">
			{#each [{ value: 'single', title: 'One photo at a time', text: 'Portrait photos get dark side bars on a wide screen.' }, { value: 'splitview', title: 'Two portraits side by side', text: 'Best when most photos come from phones. Landscape photos still show alone.' }] as option (option.value)}
				{@const selected = get('layout') === option.value}
				<button
					type="button"
					role="radio"
					aria-checked={selected}
					class="rounded-2xl border-2 p-4 text-left transition-colors {selected
						? 'border-primary bg-primary/5'
						: 'border-light-300 hover:border-primary/50'}"
					onclick={() => (general.layout = option.value)}
				>
					<div class="mb-3 flex aspect-[16/7] gap-1 rounded-lg bg-dark-900 p-2 dark:bg-black">
						{#if option.value === 'single'}
							<div class="mx-auto h-full w-3/5 rounded bg-light-500/70"></div>
						{:else}
							<div class="h-full flex-1 rounded bg-light-500/70"></div>
							<div class="h-full flex-1 rounded bg-light-400/70"></div>
						{/if}
					</div>
					<Text fontWeight="semi-bold">{option.title}</Text>
					<Text size="small" color="muted">{option.text}</Text>
				</button>
			{/each}
		</div>
		<SettingRow title="Photo size" description="Fit never crops; fill uses the whole screen">
			{#snippet control()}
				<Segmented
					label="Photo size"
					value={get('imageFill') ? 'fill' : 'fit'}
					options={[
						{ value: 'fit', label: 'Fit whole photo' },
						{ value: 'fill', label: 'Fill screen' }
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
						{ value: 'zoom', label: 'Slow zoom' },
						{ value: 'pan', label: 'Pan' },
						{ value: 'both', label: 'Zoom & pan' },
						{ value: 'still', label: 'Still' }
					]}
					onchange={setMovement}
				/>
			{/snippet}
		</SettingRow>
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
		<SettingRow
			title="Video sound"
			description="Play the sound of videos (most screens start muted)"
		>
			{#snippet control()}
				<Switch
					aria-label="Video sound"
					checked={get('playAudio')}
					onCheckedChange={(v) => (general.playAudio = v)}
				/>
			{/snippet}
		</SettingRow>
	</PageCard>

	<PageCard title="On the photo" description="What's shown in the corners." id="overlay">
		<SettingRow title="Clock" description="With today's date under it">
			{#snippet control()}
				<Switch
					aria-label="Clock"
					checked={get('showClock')}
					onCheckedChange={(v) => (general.showClock = v)}
				/>
			{/snippet}
			{#if get('showClock')}
				<div class="grid gap-x-6 sm:grid-cols-2">
					<SettingField field={generalField('clockFormat', 'Time')} {target} />
					<SettingField field={generalField('clockDateFormat', 'Date under the clock')} {target} />
				</div>
			{/if}
		</SettingRow>
		<SettingRow title="Date taken" description="Pick a look, not a format code">
			{#snippet control()}
				<Switch
					aria-label="Date taken"
					checked={get('showPhotoDate')}
					onCheckedChange={(v) => (general.showPhotoDate = v)}
				/>
			{/snippet}
			{#if get('showPhotoDate')}
				<div class="sm:w-1/2 sm:pr-3">
					<SettingField field={generalField('photoDateFormat', 'Look')} {target} />
				</div>
			{/if}
		</SettingRow>
		<SettingRow title="Place" description="Phone photos usually have one">
			{#snippet control()}
				{#if get('showImageLocation')}
					<div class="w-52">
						<Select
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
							onChange={(v) => (general.imageLocationFormat = v)}
						/>
					</div>
				{/if}
				<Switch
					aria-label="Place"
					checked={get('showImageLocation')}
					onCheckedChange={(v) => (general.showImageLocation = v)}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow title="People" description="Names of the people Immich recognised">
			{#snippet control()}
				<Switch
					aria-label="People"
					checked={get('showPeopleDesc')}
					onCheckedChange={(v) => (general.showPeopleDesc = v)}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow title="Description">
			{#snippet control()}
				<Switch
					aria-label="Description"
					checked={get('showImageDesc')}
					onCheckedChange={(v) => (general.showImageDesc = v)}
				/>
			{/snippet}
			<div class="flex items-center gap-2 text-muted">
				<Icon icon={mdiAlertOutline} size="16" class="shrink-0 text-warning" />
				<Text size="small" color="muted">Cameras often fill this with text like DJI_0073.JPG.</Text>
			</div>
		</SettingRow>
		<SettingRow title="Album name">
			{#snippet control()}
				<Switch
					aria-label="Album name"
					checked={get('showAlbumName')}
					onCheckedChange={(v) => (general.showAlbumName = v)}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow title="Tags">
			{#snippet control()}
				<Switch
					aria-label="Tags"
					checked={get('showTagsDesc')}
					onCheckedChange={(v) => (general.showTagsDesc = v)}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow title="Text colour" description="Clock and captions">
			{#snippet control()}
				{#each textColours as colour (colour)}
					{@const selected = (general.primaryColor || '#f5deb3').toLowerCase() === colour}
					<button
						type="button"
						class="h-9 w-9 rounded-full border-2 {selected
							? 'border-primary ring-2 ring-primary/40'
							: 'border-light-300'}"
						style="background: {colour}"
						aria-label="Text colour {colour}"
						aria-pressed={selected}
						onclick={() => (general.primaryColor = colour)}
					></button>
				{/each}
				<input
					type="color"
					class="h-9 w-12 cursor-pointer rounded-lg"
					aria-label="Custom text colour"
					value={general.primaryColor || '#f5deb3'}
					oninput={(e) => (general.primaryColor = e.currentTarget.value)}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow title="Box colour" description="Behind the text, when Solid or Fade is on">
			{#snippet control()}
				{#each boxColours as colour (colour)}
					{@const selected = (general.secondaryColor || '#000000').toLowerCase() === colour}
					<button
						type="button"
						class="h-9 w-9 rounded-full border-2 {selected
							? 'border-primary ring-2 ring-primary/40'
							: 'border-light-300'}"
						style="background: {colour}"
						aria-label="Box colour {colour}"
						aria-pressed={selected}
						onclick={() => (general.secondaryColor = colour)}
					></button>
				{/each}
				<input
					type="color"
					class="h-9 w-12 cursor-pointer rounded-lg"
					aria-label="Custom box colour"
					value={general.secondaryColor || '#000000'}
					oninput={(e) => (general.secondaryColor = e.currentTarget.value)}
				/>
			{/snippet}
		</SettingRow>
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
				<Select
					options={languageOptions}
					value={get('language') ?? 'en'}
					onChange={(v) => (general.language = v)}
				/>
			{/snippet}
		</SettingRow>
	</PageCard>

	<PageCard title="Weather & calendar" id="weather">
		<SettingRow
			title="Weather"
			description="Needs a free OpenWeatherMap key. Leave it empty to hide the weather."
		>
			<div class="grid gap-4 sm:grid-cols-2">
				<label class="text-sm">
					<span class="mb-1 block font-medium">API key</span>
					<PasswordInput
						autocomplete="off"
						placeholder="Paste key"
						value={general.weatherApiKey ?? ''}
						oninput={(e) => (general.weatherApiKey = e.currentTarget.value)}
					/>
				</label>
				<label class="text-sm">
					<span class="mb-1 block font-medium">Where (latitude,longitude)</span>
					<Input
						placeholder="38.72,-9.14"
						value={general.weatherLatLong ?? ''}
						oninput={(e) => (general.weatherLatLong = e.currentTarget.value)}
					/>
				</label>
			</div>
		</SettingRow>
		<SettingRow title="Units">
			{#snippet control()}
				<Segmented
					label="Units"
					value={get('unitSystem') ?? 'imperial'}
					options={[
						{ value: 'metric', label: '°C, km/h' },
						{ value: 'imperial', label: '°F, mph' }
					]}
					onchange={(v) => (general.unitSystem = String(v))}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow title="Weather description" description="e.g. “light rain” next to the temperature">
			{#snippet control()}
				<Switch
					aria-label="Weather description"
					checked={get('showWeatherDescription')}
					onCheckedChange={(v) => (general.showWeatherDescription = v)}
				/>
			{/snippet}
		</SettingRow>
		<SettingRow
			title="Calendars"
			description="Upcoming events in a corner. Paste a calendar's .ics link (iCloud: Share → Public calendar). Picked up within ~15 minutes."
		>
			<ul class="mb-3 flex flex-col gap-2">
				{#each general.webcalendars ?? [] as url, i (i)}
					<li
						class="flex items-center gap-2 rounded-xl bg-subtle/50 py-1 pr-1 pl-3 dark:bg-light-200"
					>
						<code class="min-w-0 flex-1 truncate text-sm">{url}</code>
						<IconButton
							icon={mdiDelete}
							size="small"
							variant="ghost"
							color="danger"
							aria-label="Remove calendar"
							onclick={() =>
								(general.webcalendars = (general.webcalendars ?? []).filter((_, j) => j !== i))}
						/>
					</li>
				{/each}
			</ul>
			<form
				class="flex gap-2"
				onsubmit={(e) => {
					e.preventDefault();
					addCalendar();
				}}
			>
				<Input placeholder="https://… .ics" bind:value={newCalendar} aria-label="Calendar link" />
				<Button type="submit" size="small" variant="outline" leadingIcon={mdiPlus}>Add</Button>
			</form>
		</SettingRow>
	</PageCard>
</div>
