import '@blocknote/core/fonts/inter.css';
import '@blocknote/mantine/style.css';
import './post-editor-overrides.css';

import {
    BlockNoteSchema,
    defaultBlockSpecs,
    defaultInlineContentSpecs,
    defaultStyleSpecs,
    filterSuggestionItems,
    insertOrUpdateBlock,
    PartialBlock,
} from '@blocknote/core';
import { BlockNoteView } from '@blocknote/mantine';
import {
    BasicTextStyleButton,
    BlockTypeSelect,
    ColorStyleButton,
    CreateLinkButton,
    FilePanelController,
    FormattingToolbar,
    FormattingToolbarController,
    getDefaultReactEmojiPickerItems,
    getDefaultReactSlashMenuItems,
    GridSuggestionMenuController,
    LinkToolbarController,
    NestBlockButton,
    SuggestionMenuController,
    TableHandlesController,
    TextAlignButton,
    UnnestBlockButton,
    createReactBlockSpec,
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

const embedBlock = createReactBlockSpec(
    {
        type: 'embed',
        propSchema: {
            url: { default: '' },
            title: { default: '' },
            provider: { default: 'link' },
        },
        content: 'none',
        isSelectable: true,
    },
    {
        render: ({ block }) => (
            <div className="bn-blogify-embed" data-provider={block.props.provider}>
                <span className="bn-blogify-embed-label">{labelForProvider(block.props.provider)}</span>
                <strong>{block.props.title || block.props.url || 'Embed'}</strong>
                {block.props.url ? <span>{block.props.url}</span> : null}
            </div>
        ),
        toExternalHTML: ({ block }) => (
            <a href={block.props.url} data-blogify-embed={block.props.provider}>
                {block.props.title || block.props.url}
            </a>
        ),
        parse: (element) => {
            const url = element.getAttribute('href') ?? element.dataset.url;
            if (!url) return undefined;

            return {
                url,
                title: element.textContent?.trim() ?? url,
                provider: element.dataset.blogifyEmbed ?? detectEmbedProvider(url),
            };
        },
    },
);

const blogifySchema = BlockNoteSchema.create({
    blockSpecs: {
        ...defaultBlockSpecs,
        embed: embedBlock,
    },
    inlineContentSpecs: defaultInlineContentSpecs,
    styleSpecs: defaultStyleSpecs,
});

type BlogifyEditor = typeof blogifySchema.BlockNoteEditor;

const allowedSlashTitles = new Set([
    'Paragraph',
    'Heading 1',
    'Heading 2',
    'Heading 3',
    'Bullet List',
    'Numbered List',
    'Check List',
    'Quote',
    'Code Block',
    'Table',
    'File',
    'Video',
    'Audio',
]);

function labelForProvider(provider: string): string {
    switch (provider) {
        case 'youtube':
            return 'YouTube';
        case 'vimeo':
            return 'Vimeo';
        case 'spotify':
            return 'Spotify';
        case 'codepen':
            return 'CodePen';
        default:
            return 'External link';
    }
}

function detectEmbedProvider(rawUrl: string): string {
    try {
        const host = new URL(rawUrl).hostname.toLowerCase();
        if (host.includes('youtube.com') || host.includes('youtu.be')) return 'youtube';
        if (host.includes('vimeo.com')) return 'vimeo';
        if (host.includes('spotify.com')) return 'spotify';
        if (host.includes('codepen.io')) return 'codepen';
    } catch {
        return 'link';
    }

    return 'link';
}

function openMediaLibraryModal(): void {
    const modalEl = document.getElementById('editorImageInsert-modal');
    if (!modalEl) return;
    bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

// Module-level reference so we can remove the old handler before attaching a new one,
// preventing duplicate insertions if the media item is triggered multiple times.
let activeEditorInsertHandler: ((event: Event) => void) | null = null;

function insertMediaFromLibrary(editor: BlogifyEditor): void {
    openMediaLibraryModal();

    if (activeEditorInsertHandler) {
        document.removeEventListener('mediaSelected', activeEditorInsertHandler);
        activeEditorInsertHandler = null;
    }

    const modalEl = document.getElementById('editorImageInsert-modal');
    const previouslyFocused = document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null;

    function handleMediaSelected(event: Event): void {
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

        cleanup(true);
    }

    function cleanup(restoreFocus = false): void {
        document.removeEventListener('mediaSelected', handleMediaSelected);
        if (modalEl) {
            modalEl.removeEventListener('hidden.bs.modal', onModalHidden);
        }
        activeEditorInsertHandler = null;
        if (restoreFocus) {
            window.requestAnimationFrame(() => previouslyFocused?.focus());
        }
    }

    function onModalHidden(): void {
        cleanup(true);
    }

    activeEditorInsertHandler = handleMediaSelected;
    document.addEventListener('mediaSelected', handleMediaSelected);

    if (modalEl) {
        modalEl.addEventListener('hidden.bs.modal', onModalHidden);
    }
}

function mediaLibraryItem(editor: BlogifyEditor) {
    return {
        title: 'Image from Library',
        subtext: 'Pick an existing Blogify media image',
        aliases: ['image', 'img', 'photo', 'picture', 'media', 'library'],
        group: 'Media',
        icon: <span className="bn-blogify-menu-icon" aria-hidden="true">Im</span>,
        onItemClick: () => insertMediaFromLibrary(editor),
    };
}

function embedItem(editor: BlogifyEditor) {
    return {
        title: 'Rich Embed',
        subtext: 'Embed a trusted URL or fall back to a link',
        aliases: ['embed', 'url', 'youtube', 'vimeo', 'spotify', 'codepen', 'link'],
        group: 'Media',
        icon: <span className="bn-blogify-menu-icon" aria-hidden="true">Em</span>,
        onItemClick: () => {
            const url = window.prompt('Paste an external URL to embed');
            if (!url?.trim()) return;

            const normalizedUrl = url.trim();
            const provider = detectEmbedProvider(normalizedUrl);
            insertOrUpdateBlock(editor, {
                type: 'embed',
                props: {
                    url: normalizedUrl,
                    title: provider === 'link' ? normalizedUrl : labelForProvider(provider),
                    provider,
                },
            });
        },
    };
}

function dividerItem(editor: BlogifyEditor) {
    return {
        title: 'Divider',
        subtext: 'Separate sections in the post',
        aliases: ['divider', 'separator', 'rule', 'hr', 'break'],
        group: 'Basic blocks',
        icon: <span className="bn-blogify-menu-icon" aria-hidden="true">Hr</span>,
        onItemClick: () => insertOrUpdateBlock(editor, { type: 'pageBreak' }),
    };
}

function getBloggingSlashMenuItems(editor: BlogifyEditor) {
    const defaults = getDefaultReactSlashMenuItems(editor)
        .filter((item) => allowedSlashTitles.has(item.title))
        .map((item) => {
            if (item.title === 'File') {
                return {
                    ...item,
                    subtext: 'Attach a hosted file by URL',
                };
            }

            return item;
        });

    return [
        ...defaults,
        dividerItem(editor),
        mediaLibraryItem(editor),
        embedItem(editor),
    ];
}

function BlogifyFormattingToolbar() {
    return (
        <FormattingToolbar>
            <BlockTypeSelect />
            <BasicTextStyleButton basicTextStyle="bold" />
            <BasicTextStyleButton basicTextStyle="italic" />
            <BasicTextStyleButton basicTextStyle="underline" />
            <BasicTextStyleButton basicTextStyle="strike" />
            <BasicTextStyleButton basicTextStyle="code" />
            <ColorStyleButton />
            <CreateLinkButton />
            <NestBlockButton />
            <UnnestBlockButton />
            <TextAlignButton textAlignment="left" />
            <TextAlignButton textAlignment="center" />
            <TextAlignButton textAlignment="right" />
        </FormattingToolbar>
    );
}

export default function BlockNoteEditorComponent({ initialBlocks, hiddenTextareaId, formId }: Props) {
    const editor = useCreateBlogifyEditor(initialBlocks);

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
        <BlockNoteView
            editor={editor}
            slashMenu={false}
            formattingToolbar={false}
            linkToolbar={false}
            filePanel={false}
            tableHandles={false}
            emojiPicker={false}
            theme="light">
            <FormattingToolbarController formattingToolbar={BlogifyFormattingToolbar} />
            <LinkToolbarController />
            <FilePanelController />
            <TableHandlesController />
            <SuggestionMenuController
                triggerCharacter="/"
                getItems={async (query) =>
                    filterSuggestionItems(getBloggingSlashMenuItems(editor), query)
                }
            />
            <GridSuggestionMenuController
                triggerCharacter=":"
                columns={8}
                minQueryLength={2}
                getItems={async (query) => getDefaultReactEmojiPickerItems(editor, query)}
            />
        </BlockNoteView>
    );
}

function useCreateBlogifyEditor(initialBlocks: PartialBlock[] | undefined): BlogifyEditor {
    return useCreateBlockNote({
        schema: blogifySchema,
        initialContent: initialBlocks,
    }) as BlogifyEditor;
}

function syncToHidden(editor: BlogifyEditor, hiddenTextareaId: string): void {
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
