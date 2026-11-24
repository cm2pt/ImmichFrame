<script lang="ts">
	interface Props {
		title: string;
		value: string | null | undefined;
		placeholder?: string;
		type?: 'text' | 'password' | 'url';
		autocomplete?: HTMLInputElement['autocomplete'];
		oninput: (value: string) => void;
		id?: string;
		/** Long paths: show the start and the end ("/run/…/immich.key") until the field is edited. */
		truncate?: 'middle';
	}

	let {
		title,
		value,
		placeholder = '',
		type = 'text',
		autocomplete = 'off',
		oninput,
		id,
		truncate
	}: Props = $props();
	const uid = $props.id();
	let focused = $state(false);
	// Long paths, while not being edited: the folder shrinks with an ellipsis and the file name
	// always shows ("/private/tmp/c…/immich.key"), by the room there is, like Finder.
	const parts = $derived.by(() => {
		const v = value ?? '';
		if (truncate !== 'middle' || focused || !v) return null;
		const cut = v.lastIndexOf('/');
		return cut > 0 ? { folder: v.slice(0, cut), file: v.slice(cut) } : null;
	});
</script>

<!-- A label on the left and a borderless, right-aligned field: iOS Settings' text rows. -->
<div
	class="border-sep flex min-h-11 items-center gap-4 border-t py-1 first:border-t-0"
	data-setting={title}
	{id}
>
	<label for="{uid}-field" class="shrink-0 text-[0.9375rem] md:text-[0.875rem]">{title}</label>
	<span class="relative flex min-w-0 flex-1">
		<input
			id="{uid}-field"
			{type}
			{placeholder}
			{autocomplete}
			value={value ?? ''}
			title={truncate && value ? value : undefined}
			onfocus={() => (focused = true)}
			onblur={() => (focused = false)}
			oninput={(e) => oninput(e.currentTarget.value)}
			class="min-w-0 flex-1 bg-transparent py-2 text-right text-[0.9375rem] outline-none placeholder:text-[#8e8e93] focus:text-left md:text-[0.875rem] {parts
				? 'text-transparent'
				: ''}"
		/>
		{#if parts}
			<span
				aria-hidden="true"
				class="pointer-events-none absolute inset-0 flex min-w-0 items-center justify-end text-[0.9375rem] md:text-[0.875rem]"
			>
				<span class="min-w-0 truncate">{parts.folder}</span><span class="shrink-0"
					>{parts.file}</span
				>
			</span>
		{/if}
	</span>
</div>
