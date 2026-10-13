type BrandLogoProps = { size?: number };

/** Logo DCMS dùng chung cho sidebar và panel thương hiệu. */
export default function BrandLogo({ size = 22 }: BrandLogoProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" fill="none" aria-hidden>
      <path d="M16 6.5c-4.6 0-8.3 2.7-8.3 6 0 2.1 1.4 3.5 2.7 4.7.9.9 1.5 1.4 1.5 2.6v1.4c0 1 .8 1.7 1.7 1.7h1.2v2.5c0 .8.6 1.4 1.4 1.4s1.4-.6 1.4-1.4v-2.5h1.2V25c0 .8.6 1.4 1.4 1.4s1.4-.6 1.4-1.4v-2.5h1.2c1 0 1.7-.8 1.7-1.7v-1.4c0-1.2.6-1.7 1.5-2.6 1.3-1.2 2.7-2.6 2.7-4.7 0-3.3-3.7-6-8.3-6h-3.4z" fill="currentColor" />
    </svg>
  );
}
