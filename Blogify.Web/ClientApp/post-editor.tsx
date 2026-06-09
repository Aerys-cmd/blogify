import React from 'react';
import ReactDOM from 'react-dom/client';
import BlockNoteEditor from './BlockNoteEditor';

type InitialBlocks = Parameters<typeof BlockNoteEditor>[0]['initialBlocks'];

export async function initPostEditor(
    wrapperId: string,
    hiddenTextareaId: string,
    formId: string,
): Promise<void> {
    const wrapperEl = document.getElementById(wrapperId);
    const hiddenTextareaEl = document.getElementById(hiddenTextareaId) as HTMLTextAreaElement | null;

    if (!wrapperEl || !hiddenTextareaEl) return;

    const rawContent = hiddenTextareaEl.value?.trim() ?? '';
    let initialBlocks: InitialBlocks = undefined;
    if (rawContent) {
        try {
            const parsed = JSON.parse(rawContent);
            if (Array.isArray(parsed) && parsed.length > 0) {
                initialBlocks = parsed as InitialBlocks;
            }
        } catch {
            initialBlocks = undefined;
        }
    }

    // Tell jQuery validate not to ignore the hidden textarea
    if ((window as Window & { jQuery?: JQuery }).jQuery) {
        const jq = (window as Window & { jQuery?: JQuery }).jQuery!;
        if ((jq as unknown as { validator?: { setDefaults: (opts: unknown) => void } }).validator) {
            (jq as unknown as { validator: { setDefaults: (opts: unknown) => void } }).validator.setDefaults({
                ignore: `:hidden:not(#${hiddenTextareaId})`,
            });
        }
    }

    const root = ReactDOM.createRoot(wrapperEl);
    root.render(
        <React.StrictMode>
            <BlockNoteEditor
                initialBlocks={initialBlocks}
                hiddenTextareaId={hiddenTextareaId}
                formId={formId}
            />
        </React.StrictMode>,
    );
}
