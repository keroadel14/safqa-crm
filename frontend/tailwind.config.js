/** @type {import('tailwindcss').Config} */
export default {
  darkMode: "class",
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    screens: { sm: "640px", md: "768px", lg: "1024px", xl: "1280px", "2xl": "1536px", "3xl": "1920px" },
    extend: {
      colors: {
        primary: { DEFAULT: "#0E7490", hover: "#155E75" },
        secondary: "#0F172A", accent: "#EA580C",
        surface: { DEFAULT: "#F8FAFB", dark: "#0B0F19" },
        card: { DEFAULT: "#FFFFFF", dark: "#111827" },
      },
      fontFamily: { sans: ['"Inter Variable"', '"Noto Sans Arabic Variable"', "system-ui", "sans-serif"], mono: ["JetBrains Mono", "ui-monospace", "monospace"] },
    },
  },
  plugins: [],
};
