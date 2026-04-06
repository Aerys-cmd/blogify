/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Areas/Blog/Themes/{Minimal,Aurora}/**/*.cshtml',
  ],
  theme: {
    extend: {},
  },
  plugins: [
    require('@tailwindcss/typography'),
  ],
}
