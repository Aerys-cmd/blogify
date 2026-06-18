/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Areas/Blog/Pages/**/*.cshtml',
    './Areas/Blog/Themes/**/*.cshtml',
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Source Sans 3"', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        serif: ['Georgia', 'Cambria', '"Times New Roman"', 'serif'],
        mono: ['ui-monospace', 'SFMono-Regular', 'Menlo', 'Consolas', 'monospace'],
      },
      colors: {
        ink: '#17191F',
        canvas: '#F6F7F5',
        surface: '#FFFFFF',
        subtle: '#ECEEEA',
        line: '#D8DCD6',
        muted: '#626970',
        brand: '#B83A18',
        bright: '#F06432',
      },
    },
  },
  plugins: [
    require('@tailwindcss/typography'),
  ],
}
