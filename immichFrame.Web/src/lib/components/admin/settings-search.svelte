<script lang="ts" module>
	export interface SearchHit {
		title: string;
		page: string;
		pageTitle: string;
	}

	// Every setting by the title its row shows, with a few words people might type instead.
	const index: { title: string; page: string; words?: string }[] = [
		{ title: 'Favorites', page: 'photos', words: 'heart starred' },
		{ title: 'In any album', page: 'photos', words: 'all albums every album' },
		{ title: 'Full resolution', page: 'look', words: 'quality sharp 4k original high resolution' },
		{ title: 'Memories', page: 'photos', words: 'on this day birthdays' },
		{ title: 'Albums', page: 'photos' },
		{ title: 'People', page: 'photos', words: 'faces persons' },
		{ title: 'Tags', page: 'photos' },
		{ title: 'Never show', page: 'photos', words: 'hide exclude albums people tags screenshots' },
		{ title: 'Order', page: 'look', words: 'shuffle random chronological oldest newest repeat' },
		{ title: 'Ages', page: 'look', words: 'age birthday years old' },
		{ title: 'Show events for', page: 'look', words: 'calendar days ahead week upcoming' },
		{ title: 'Taken', page: 'photos', words: 'date range year' },
		{ title: 'Minimum rating', page: 'photos', words: 'stars' },
		{ title: 'Videos', page: 'photos' },
		{ title: 'Archived photos', page: 'photos', words: 'archive' },
		{ title: 'Time on each photo', page: 'look', words: 'interval seconds duration speed' },
		{ title: 'Change between photos', page: 'look', words: 'transition fade' },
		{ title: 'Progress bar', page: 'look' },
		{ title: 'Layout', page: 'look', words: 'split view portrait two photos' },
		{ title: 'Photo size', page: 'look', words: 'fit fill crop' },
		{ title: 'Movement', page: 'look', words: 'zoom pan ken burns' },
		{ title: 'Video sound', page: 'look', words: 'audio mute' },
		{ title: 'Clock', page: 'look', words: 'time format' },
		{ title: 'Date taken', page: 'look', words: 'photo date format' },
		{ title: 'Place', page: 'look', words: 'location city country' },
		{ title: 'Album name', page: 'look' },
		{ title: 'Description', page: 'look', words: 'caption' },
		{
			title: 'Behind the clock and captions',
			page: 'look',
			words: 'style blur frosted solid background'
		},
		{ title: 'Text color', page: 'look', words: 'colour primary' },
		{ title: 'Box color', page: 'look', words: 'colour background secondary' },
		{ title: 'Text size', page: 'look', words: 'font size bigger' },
		{ title: 'Language', page: 'look', words: 'locale' },
		{ title: 'Weather', page: 'look', words: 'openweathermap temperature' },
		{ title: 'Calendars', page: 'look', words: 'ics events appointments' },
		{ title: 'Add a screen', page: 'screens', words: 'device apple tv ipad qr code new' },
		{ title: 'Immich accounts', page: 'access', words: 'api key server account connection' },
		{ title: 'Frame secret', page: 'access', words: 'authentication password screens' },
		{ title: 'Admin password', page: 'access', words: 'login sign in' },
		{ title: 'Keep images on disk', page: 'access', words: 'cache download' },
		{ title: 'Refresh albums and people', page: 'access', words: 'interval' },
		{ title: 'Webhook', page: 'access', words: 'home assistant automation' },
		{ title: 'Recent changes', page: 'access', words: 'history undo restore' },
		{ title: 'Backup', page: 'access', words: 'download export import restore' },
		{ title: 'Preview', page: 'look', words: 'live preview' },
		{ title: 'Up next', page: 'photos', words: 'sample shuffle' }
	];

	const pageTitles: Record<string, string> = {
		photos: 'Photos',
		look: 'Slideshow & Look',
		screens: 'Screens',
		access: 'Access & Accounts'
	};
</script>

<script lang="ts">
	import Glyph from './glyph.svelte';

	interface Props {
		onpick: (hit: SearchHit) => void;
		autofocus?: boolean;
	}

	let { onpick, autofocus = false }: Props = $props();

	let query = $state('');
	let active = $state(0);
	let input: HTMLInputElement | undefined = $state();

	let hits: SearchHit[] = $derived.by(() => {
		const q = query.trim().toLowerCase();
		if (!q) return [];
		return index
			.map((entry) => {
				const title = entry.title.toLowerCase();
				const score = title.startsWith(q)
					? 0
					: title.includes(q)
						? 1
						: entry.words?.includes(q)
							? 2
							: -1;
				return { entry, score };
			})
			.filter((r) => r.score >= 0)
			.sort((a, b) => a.score - b.score)
			.slice(0, 8)
			.map(({ entry }) => ({
				title: entry.title,
				page: entry.page,
				pageTitle: pageTitles[entry.page]
			}));
	});

	$effect(() => {
		void hits;
		active = 0;
	});

	$effect(() => {
		if (autofocus) input?.focus();
	});

	function pick(hit: SearchHit) {
		query = '';
		input?.blur();
		onpick(hit);
	}

	function onKeydown(e: KeyboardEvent) {
		if (e.key === 'ArrowDown') {
			e.preventDefault();
			active = Math.min(active + 1, hits.length - 1);
		} else if (e.key === 'ArrowUp') {
			e.preventDefault();
			active = Math.max(active - 1, 0);
		} else if (e.key === 'Enter' && hits[active]) {
			e.preventDefault();
			pick(hits[active]);
		} else if (e.key === 'Escape') {
			query = '';
		}
	}
</script>

<div class="relative">
	<div class="bg-fill flex items-center gap-1.5 rounded-lg px-2 py-1.5">
		<Glyph name="search" size={15} class="text-label-2" />
		<input
			bind:this={input}
			bind:value={query}
			type="search"
			placeholder="Search"
			aria-label="Search settings"
			role="combobox"
			aria-expanded={hits.length > 0}
			aria-controls="settings-search-results"
			aria-activedescendant={hits.length ? `search-hit-${active}` : undefined}
			autocomplete="off"
			class="w-full min-w-0 bg-transparent text-[0.875rem] outline-none placeholder:text-[#6e6e73] [&::-webkit-search-cancel-button]:hidden dark:placeholder:text-[#a1a1a6]"
			onkeydown={onKeydown}
		/>
		{#if query}
			<button
				type="button"
				class="text-label-2 rounded-full p-0.5"
				aria-label="Clear search"
				onclick={() => (query = '')}
			>
				<Glyph name="x" size={13} stroke={2.2} />
			</button>
		{/if}
	</div>
	{#if query.trim()}
		<ul
			id="settings-search-results"
			role="listbox"
			class="bg-group border-sep absolute inset-x-0 top-full z-40 mt-1.5 overflow-hidden rounded-xl border p-1 shadow-xl"
		>
			{#each hits as hit, i (hit.page + hit.title)}
				<li id="search-hit-{i}" role="option" aria-selected={i === active}>
					<button
						type="button"
						class="flex w-full flex-col rounded-lg px-2.5 py-1.5 text-left {i === active
							? 'bg-primary text-white dark:text-black'
							: ''}"
						onmouseenter={() => (active = i)}
						onclick={() => pick(hit)}
					>
						<span class="text-[0.875rem] font-medium">{hit.title}</span>
						<span class="text-[0.75rem] {i === active ? 'opacity-80' : 'text-label-2'}"
							>{hit.pageTitle}</span
						>
					</button>
				</li>
			{:else}
				<li class="text-label-2 px-2.5 py-2 text-[0.8125rem]">No results</li>
			{/each}
		</ul>
	{/if}
</div>
