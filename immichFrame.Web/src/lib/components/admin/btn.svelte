<script lang="ts">
	import type { Snippet } from 'svelte';
	import { Icon } from '@immich/ui';

	// The admin's button: same props as @immich/ui's Button, drawn the way Apple's settings
	// draw them: a filled accent pill for the main action, soft gray pills for the rest.
	interface Props {
		variant?: 'filled' | 'outline' | 'ghost';
		color?: 'primary' | 'secondary' | 'danger' | 'success';
		size?: 'tiny' | 'small' | 'medium';
		leadingIcon?: string;
		trailingIcon?: string;
		loading?: boolean;
		disabled?: boolean;
		fullWidth?: boolean;
		type?: 'button' | 'submit';
		href?: string;
		target?: string;
		id?: string;
		title?: string;
		class?: string;
		'aria-label'?: string;
		'aria-expanded'?: boolean;
		tabindex?: number;
		onclick?: (e: MouseEvent) => void;
		children?: Snippet;
		ref?: HTMLElement | null;
	}

	let {
		variant = 'filled',
		color = 'primary',
		size = 'medium',
		leadingIcon,
		trailingIcon,
		loading = false,
		disabled = false,
		fullWidth = false,
		type = 'button',
		href,
		target,
		class: className = '',
		children,
		ref = $bindable(null),
		...rest
	}: Props = $props();

	const sizes = {
		tiny: 'h-7 gap-1 px-2.5 text-[0.75rem]',
		small: 'h-8 gap-1.5 px-3.5 text-[0.8125rem]',
		medium: 'h-9 gap-1.5 px-4 text-[0.875rem]'
	};
	const iconSize = { tiny: '14', small: '16', medium: '17' };

	let look = $derived.by(() => {
		if (variant === 'filled') {
			if (color === 'danger') return 'bg-[#ff3b30] text-white hover:brightness-110';
			if (color === 'secondary') return 'bg-fill hover:brightness-95 dark:hover:brightness-125';
			return 'bg-primary text-white hover:brightness-110 dark:text-black';
		}
		if (variant === 'outline') {
			const text =
				color === 'danger'
					? 'text-danger-700 dark:text-danger-400'
					: color === 'primary'
						? 'text-primary'
						: '';
			return `bg-fill ${text} hover:brightness-95 dark:hover:brightness-125`;
		}
		const text =
			color === 'danger'
				? 'text-danger-700 dark:text-danger-400'
				: color === 'primary'
					? 'text-primary'
					: '';
		return `${text} hover:bg-black/5 dark:hover:bg-white/10`;
	});

	let classes = $derived(
		`inline-flex shrink-0 items-center justify-center rounded-full font-medium whitespace-nowrap transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary disabled:pointer-events-none disabled:opacity-40 ${sizes[size]} ${look} ${fullWidth ? 'w-full' : ''} ${className}`
	);
</script>

{#snippet content()}
	{#if loading}
		<svg
			class="animate-spin"
			width={iconSize[size]}
			height={iconSize[size]}
			viewBox="0 0 24 24"
			aria-hidden="true"
		>
			<circle
				cx="12"
				cy="12"
				r="9"
				fill="none"
				stroke="currentColor"
				stroke-width="3"
				opacity="0.25"
			/>
			<path
				d="M21 12a9 9 0 0 0-9-9"
				fill="none"
				stroke="currentColor"
				stroke-width="3"
				stroke-linecap="round"
			/>
		</svg>
	{:else if leadingIcon}
		<Icon icon={leadingIcon} size={iconSize[size]} />
	{/if}
	{@render children?.()}
	{#if trailingIcon}<Icon icon={trailingIcon} size={iconSize[size]} />{/if}
{/snippet}

{#if href}
	<!-- Callers pass a resolved or external link. -->
	<!-- eslint-disable svelte/no-navigation-without-resolve -->
	<a
		bind:this={ref}
		{href}
		{target}
		rel={target === '_blank' ? 'noreferrer' : undefined}
		class={classes}
		{...rest}
	>
		{@render content()}
	</a>
	<!-- eslint-enable svelte/no-navigation-without-resolve -->
{:else}
	<button
		{type}
		class={classes}
		disabled={disabled || loading}
		aria-busy={loading || undefined}
		{...rest}
	>
		{@render content()}
	</button>
{/if}
