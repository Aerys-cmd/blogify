import '@blocknote/core/fonts/inter.css';
import '@blocknote/mantine/style.css';
import './post-editor-overrides.css';

import { BlockNoteEditor as BNEditor, filterSuggestionItems, PartialBlock } from '@blocknote/core';
import { BlockNoteView } from '@blocknote/mantine';
import {
    getDefaultReactSlashMenuItems,
    SuggestionMenuController,
    useCreateBlockNote,
} from '@blocknote/react';
import React, { useEffect } from 'react';

// BootstrapModal type shim — available on window.bootstrap in the BlogAdmin layout
declare const bootstrap: { Modal: { getOrCreateInstance(el: Element): { show(): void } } };

interface Props {
    initialBlocks: PartialBlock[] | undefined;
    hiddenTextareaId: string;
    formId: string;
}

function openMediaLibraryModal(): void {
    const modalEl = document.getElementById('editorImageInsert-modal');
    if (!modalEl) return;
    bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function customImageItem(editor: BNEditor) {
    return {
        title: 'Image from Library',
        subtext: 'Pick from media library',
        aliases: ['image', 'img', 'photo', 'picture'],
        group: 'Media',
        icon: <span aria-hidden="true">🖼</span>,
        onItemClick: () => {
            openMediaLibraryModal();

            // mediaSelected is dispatched by media-picker.js when the user picks an image
            document.addEventListener(
                'mediaSelected',
                (event: Event) => {
                    const e = event as CustomEvent<{
                        targetInputId: string;
                        url: string;
                        fullUrl?: string;
                        altText?: string;
                    }>;
                    if (e.detail.targetInputId !== 'editorImageInsert') return;

                    const url = e.detail.fullUrl ?? e.detail.url;
                    const cursorBlock = editor.getTextCursorPosition().block;
                    editor.insertBlocks(
                        [{ type: 'image', props: { url, caption: e.detail.altText ?? '' } }],
                        cursorBlock,
                        'after',
                    );
                },
                { once: true },
            );
        },
    };
}

export default function BlockNoteEditorComponent({ initialBlocks, hiddenTextareaId, formId }: Props) {
    const editor = useCreateBlockNote({
        initialContent: initialBlocks,
    });

    // Sync editor content to the hidden textarea whenever blocks change
    useEffect(() => {
        const unsubscribe = editor.onChange(() => {
            syncToHidden(editor, hiddenTextareaId);
        });
        // Sync once on mount so the textarea has the initial value
        syncToHidden(editor, hiddenTextareaId);
        return unsubscribe;
    }, [editor, hiddenTextareaId]);

    // Ensure the latest content is in the textarea right before form submission
    useEffect(() => {
        const formEl = document.getElementById(formId);
        if (!formEl) return;

        function handleSubmit() {
            syncToHidden(editor, hiddenTextareaId);
        }

        formEl.addEventListener('submit', handleSubmit);
        return () => formEl.removeEventListener('submit', handleSubmit);
    }, [editor, hiddenTextareaId, formId]);

    return (
        <BlockNoteView editor={editor} slashMenu={false} theme="light">
            <SuggestionMenuController
                triggerCharacter="/"
                getItems={async (query) =>
                    filterSuggestionItems(
                        [...getDefaultReactSlashMenuItems(editor), customImageItem(editor)],
                        query,
                    )
                }
            />
        </BlockNoteView>
    );
}

function syncToHidden(editor: BNEditor, hiddenTextareaId: string): void {
    const textarea = document.getElementById(hiddenTextareaId) as HTMLTextAreaElement | null;
    if (!textarea) return;

    textarea.value = JSON.stringify(editor.document);

    // Trigger jQuery validation on the hidden field if available
    const jqWindow = window as Window & { jQuery?: (selector: string) => { valid?(): boolean } };
    if (jqWindow.jQuery) {
        const $field = jqWindow.jQuery(`#${hiddenTextareaId}`);
        if ($field.valid) $field.valid();
    }
}
