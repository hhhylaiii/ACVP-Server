// Small inline SVG icons (no external assets — the tool must run fully offline).

interface IconProps {
  className?: string;
}

function svgProps(className?: string) {
  return {
    className,
    width: 20,
    height: 20,
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 2,
    strokeLinecap: 'round',
    strokeLinejoin: 'round',
    'aria-hidden': true,
  } as const;
}

export function IconShield({ className }: IconProps) {
  return (
    <svg {...svgProps(className)}>
      <path d="M12 2l8 3.5v5.2c0 5-3.4 9.3-8 10.8-4.6-1.5-8-5.8-8-10.8V5.5L12 2z" />
      <path d="M9 12l2 2 4-4.5" />
    </svg>
  );
}

export function IconKey({ className }: IconProps) {
  return (
    <svg {...svgProps(className)}>
      <circle cx="8" cy="15" r="4" />
      <path d="M10.8 12.2L20 3M15 5l3 3M12 8l3 3" />
    </svg>
  );
}

export function IconSignature({ className }: IconProps) {
  return (
    <svg {...svgProps(className)}>
      <path d="M17 3l4 4L8 20H4v-4L17 3z" />
      <path d="M3 21h18" strokeWidth={1.5} />
    </svg>
  );
}

export function IconCheck({ className }: IconProps) {
  return (
    <svg {...svgProps(className)}>
      <path d="M4 12.5l5 5L20 6.5" />
    </svg>
  );
}

export function IconLock({ className }: IconProps) {
  return (
    <svg {...svgProps(className)}>
      <rect x="4" y="11" width="16" height="10" rx="2" />
      <path d="M8 11V7a4 4 0 018 0v4" />
    </svg>
  );
}

export function IconUpload({ className }: IconProps) {
  return (
    <svg {...svgProps(className)}>
      <path d="M12 16V4M7 9l5-5 5 5" />
      <path d="M4 20h16" />
    </svg>
  );
}

export function IconFile({ className }: IconProps) {
  return (
    <svg {...svgProps(className)}>
      <path d="M14 2H7a2 2 0 00-2 2v16a2 2 0 002 2h10a2 2 0 002-2V7l-5-5z" />
      <path d="M14 2v5h5" />
    </svg>
  );
}

export function IconAlert({ className }: IconProps) {
  return (
    <svg {...svgProps(className)}>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 8v5M12 16.5v.5" />
    </svg>
  );
}
