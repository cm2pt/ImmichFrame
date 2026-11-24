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
	const shown = $derived.by(() => {
		const v = value ?? '';
		if (truncate !== 'middle' || focused || v.length <= 36) return v;
		return `${v.slice(0, 14)}…${v.slice(-20)}`;
	});
</script>

<!-- A label on the left and a borderless, right-aligned field: iOS Settings' text rows. -->
<div
	class="border-sep flex min-h-11 items-center gap-4 border-t py-1 first:border-t-0"
	data-setting={title}
	{id}
>
	<label for="{uid}-field" class="shrink-0 text-[0.9375rem] md:text-[0.875rem]">{title}</label>
	<input
		id="{uid}-field"
		{type}
		{placeholder}
		{autocomplete}
		value={shown}
		title={truncate && value ? value : undefined}
		onfocus={() => (focused = true)}
		onblur={() => (focused = false)}
		oninput={(e) => oninput(e.currentTarget.value)}
		class="min-w-0 flex-1 bg-transparent py-2 text-right text-[0.9375rem] outline-none placeholder:text-[#8e8e93] focus:text-left md:text-[0.875rem]"
	/>
</div>
