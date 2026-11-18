<script lang="ts">
	interface Props {
		checked: boolean | null | undefined;
		onCheckedChange: (checked: boolean) => void;
		'aria-label': string;
		disabled?: boolean;
	}

	let { checked, onCheckedChange, 'aria-label': label, disabled = false }: Props = $props();
</script>

<!-- An iOS-style switch: 51×31, the thumb slides, the track fills with the accent. -->
<button
	type="button"
	role="switch"
	aria-checked={!!checked}
	aria-label={label}
	{disabled}
	class="relative inline-flex h-[31px] w-[51px] shrink-0 items-center rounded-full transition-colors duration-200 ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary disabled:opacity-40 {checked
		? 'bg-accent'
		: 'bg-fill'}"
	onclick={() => onCheckedChange(!checked)}
>
	<span
		class="absolute left-[2px] h-[27px] w-[27px] rounded-full bg-white shadow-[0_3px_8px_rgba(0,0,0,0.15),0_3px_1px_rgba(0,0,0,0.06)] transition-transform duration-200 ease-out {checked
			? 'translate-x-[20px]'
			: ''}"
	></span>
</button>
