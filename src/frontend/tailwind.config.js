/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{vue,js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        primary: '#0ea5e9',
        secondary: '#14b8a6',
        dark: {
          900: '#0f172a',
          800: '#1e293b',
          700: '#334155'
        }
      }
    },
  },
  plugins: [],
}
