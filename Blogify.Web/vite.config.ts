import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
    plugins: [react()],
    define: {
        'process.env.NODE_ENV': JSON.stringify('production'),
    },
    build: {
        lib: {
            entry: path.resolve(__dirname, 'ClientApp/post-editor.tsx'),
            formats: ['es'],
            fileName: () => 'js/post-editor.js',
        },
        outDir: 'wwwroot',
        emptyOutDir: false,
        rollupOptions: {
            output: {
                // Inline all dynamic imports into a single ES module file so
                // wwwroot/js/ stays clean with no hashed chunk files.
                inlineDynamicImports: true,
                assetFileNames: (assetInfo) => {
                    if (assetInfo.names?.some((n) => n.endsWith('.css'))) {
                        return 'css/post-editor.css';
                    }
                    return 'assets/[name][extname]';
                },
            },
        },
    },
});


