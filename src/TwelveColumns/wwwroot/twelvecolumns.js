// TwelveColumns — pointer gesture layer for the CanvasGrid component.
//
// Design notes
// ------------
// The server owns the layout. This module only translates pointer movement into grid
// coordinates and calls back into .NET when the *target cell* changes — not on every pixel —
// so a Blazor Server circuit sees a handful of messages per second rather than hundreds.
//
// Between those callbacks the widget is moved locally so the gesture feels immediate. The
// live offset is written to `.tc-item-shell`, an inner element Blazor never renders a style
// attribute for; writing it to `.tc-item` itself would be wiped by the next render, because
// Blazor rewrites that element's style attribute whenever the widget's cell changes.

const NO_DRAG_SELECTOR = 'button, a, input, select, textarea, label, [contenteditable="true"], [data-tc-nodrag]';
const DRAG_THRESHOLD_PX = 3;

/** @type {WeakMap<HTMLElement, GridState>} */
const grids = new WeakMap();

class GridState {
    constructor(element, dotNet, options) {
        this.element = element;
        this.dotNet = dotNet;
        this.options = options;
        this.gesture = null;
        this.frame = 0;

        this.onPointerDown = (e) => this.handlePointerDown(e);
        this.onPointerMove = (e) => this.handlePointerMove(e);
        this.onPointerUp = (e) => this.handlePointerUp(e);
        this.onDragOver = (e) => this.handleDragOver(e);
        this.onDragLeave = (e) => this.handleDragLeave(e);
        this.onDrop = (e) => this.handleDrop(e);

        element.addEventListener('pointerdown', this.onPointerDown);
        element.addEventListener('dragover', this.onDragOver);
        element.addEventListener('dragleave', this.onDragLeave);
        element.addEventListener('drop', this.onDrop);
    }

    detach() {
        this.endGesture(false);
        this.element.removeEventListener('pointerdown', this.onPointerDown);
        this.element.removeEventListener('dragover', this.onDragOver);
        this.element.removeEventListener('dragleave', this.onDragLeave);
        this.element.removeEventListener('drop', this.onDrop);
    }

    /** Content-box geometry of the canvas, in pixels. */
    metrics() {
        const style = getComputedStyle(this.element);
        const rect = this.element.getBoundingClientRect();
        const left = rect.left + parseFloat(style.paddingLeft || '0');
        const top = rect.top + parseFloat(style.paddingTop || '0');
        const width = rect.width
            - parseFloat(style.paddingLeft || '0')
            - parseFloat(style.paddingRight || '0');

        const { columns, gap, rowHeight } = this.options;
        const columnWidth = Math.max(1, (width - (columns - 1) * gap) / columns);

        return {
            left,
            top,
            columnWidth,
            stepX: columnWidth + gap,
            stepY: rowHeight + gap,
        };
    }

    /** The widget element for an id, re-queried each time because Blazor may replace it. */
    itemElement(id) {
        return this.element.querySelector(`.tc-item[data-tc-id="${CSS.escape(id)}"]`);
    }

    handlePointerDown(e) {
        if (!this.options.enabled || this.gesture || e.button !== 0) {
            return;
        }

        const item = e.target.closest('.tc-item');
        if (!item || !this.element.contains(item)) {
            return;
        }

        const resizeHandle = e.target.closest('[data-tc-resize]');
        const dragHandle = resizeHandle ? null : e.target.closest('[data-tc-drag]');

        if (!resizeHandle && !dragHandle) {
            return;
        }

        // Never hijack a click that belongs to interactive content inside the widget.
        if (!resizeHandle && e.target.closest(NO_DRAG_SELECTOR)) {
            return;
        }

        const dataset = item.dataset;
        this.gesture = {
            id: dataset.tcId,
            mode: resizeHandle ? 'resize' : 'drag',
            dir: resizeHandle ? resizeHandle.dataset.tcResize : '',
            pointerId: e.pointerId,
            startClientX: e.clientX,
            startClientY: e.clientY,
            startX: Number(dataset.tcX),
            startY: Number(dataset.tcY),
            startW: Number(dataset.tcW),
            startH: Number(dataset.tcH),
            minW: Number(dataset.tcMinw) || 1,
            minH: Number(dataset.tcMinh) || 1,
            maxW: dataset.tcMaxw ? Number(dataset.tcMaxw) : this.options.columns,
            maxH: dataset.tcMaxh ? Number(dataset.tcMaxh) : Number.MAX_SAFE_INTEGER,
            metrics: this.metrics(),
            active: false,
            sentX: Number(dataset.tcX),
            sentY: Number(dataset.tcY),
            sentW: Number(dataset.tcW),
            sentH: Number(dataset.tcH),
            pending: null,
        };

        // Listening on window rather than the handle keeps the gesture alive across the
        // re-renders that Blazor performs mid-drag, which can replace the handle element.
        window.addEventListener('pointermove', this.onPointerMove, { passive: false });
        window.addEventListener('pointerup', this.onPointerUp);
        window.addEventListener('pointercancel', this.onPointerUp);
    }

    handlePointerMove(e) {
        const g = this.gesture;
        if (!g || e.pointerId !== g.pointerId) {
            return;
        }

        const dx = e.clientX - g.startClientX;
        const dy = e.clientY - g.startClientY;

        if (!g.active) {
            if (Math.abs(dx) < DRAG_THRESHOLD_PX && Math.abs(dy) < DRAG_THRESHOLD_PX) {
                return;
            }
            g.active = true;
            document.body.classList.add('tc-gesture-active');
            this.dotNet.invokeMethodAsync('JsInteractionStart', g.id, g.mode);
        }

        e.preventDefault();
        g.pending = { dx, dy };

        if (!this.frame) {
            this.frame = requestAnimationFrame(() => {
                this.frame = 0;
                if (this.gesture && this.gesture.pending) {
                    this.applyMove(this.gesture.pending.dx, this.gesture.pending.dy);
                }
            });
        }
    }

    applyMove(dx, dy) {
        const g = this.gesture;
        const el = this.itemElement(g.id);
        if (!el) {
            return;
        }

        const { stepX, stepY, columnWidth } = g.metrics;
        const { gap, rowHeight, columns } = this.options;
        const target = g.mode === 'drag'
            ? this.dragTarget(g, dx, dy, stepX, stepY, columns)
            : this.resizeTarget(g, dx, dy, stepX, stepY, columns);

        // The element currently sits wherever the server put it, which may already differ
        // from where the pointer is. Offset from that, not from the gesture's start cell,
        // so the widget stays glued to the cursor while neighbours shuffle underneath.
        const liveX = Number(el.dataset.tcX);
        const liveY = Number(el.dataset.tcY);

        const shell = el.querySelector('.tc-item-shell');
        if (shell) {
            const offsetX = target.px.left - (liveX - g.startX) * stepX;
            const offsetY = target.px.top - (liveY - g.startY) * stepY;
            shell.style.transform = `translate(${offsetX}px, ${offsetY}px)`;

            if (g.mode === 'resize') {
                // Size is measured from the gesture's start rather than the element's
                // current width: the server may already have applied part of the resize,
                // and adding the pointer delta to that would double-count it.
                const startWidth = g.startW * columnWidth + (g.startW - 1) * gap;
                const startHeight = g.startH * rowHeight + (g.startH - 1) * gap;
                shell.style.width = `${Math.max(columnWidth, startWidth + target.px.width)}px`;
                shell.style.height = `${Math.max(rowHeight, startHeight + target.px.height)}px`;
            }
        }

        const changed = g.mode === 'drag'
            ? target.x !== g.sentX || target.y !== g.sentY
            : target.x !== g.sentX || target.y !== g.sentY || target.w !== g.sentW || target.h !== g.sentH;

        if (!changed) {
            return;
        }

        g.sentX = target.x;
        g.sentY = target.y;
        g.sentW = target.w;
        g.sentH = target.h;

        if (g.mode === 'drag') {
            this.dotNet.invokeMethodAsync('JsDragMove', g.id, target.x, target.y);
        } else {
            this.dotNet.invokeMethodAsync('JsResizeMove', g.id, target.x, target.y, target.w, target.h);
        }
    }

    dragTarget(g, dx, dy, stepX, stepY, columns) {
        const x = clamp(Math.round(g.startX + dx / stepX), 0, Math.max(0, columns - g.startW));
        const y = Math.max(0, Math.round(g.startY + dy / stepY));
        return { x, y, w: g.startW, h: g.startH, px: { left: dx, top: dy, width: 0, height: 0 } };
    }

    resizeTarget(g, dx, dy, stepX, stepY, columns) {
        const dir = g.dir;
        let x = g.startX;
        let y = g.startY;
        let w = g.startW;
        let h = g.startH;

        if (dir.includes('e')) {
            w = g.startW + Math.round(dx / stepX);
        }
        if (dir.includes('s')) {
            h = g.startH + Math.round(dy / stepY);
        }
        if (dir.includes('w')) {
            const shift = Math.round(dx / stepX);
            x = g.startX + shift;
            w = g.startW - shift;
        }
        if (dir.includes('n')) {
            const shift = Math.round(dy / stepY);
            y = g.startY + shift;
            h = g.startH - shift;
        }

        // Clamp the size first, then pull the origin back from the far edge so a west/north
        // grip stops moving as soon as the widget hits its minimum size.
        const maxW = Math.min(g.maxW, columns);
        w = clamp(w, Math.max(1, g.minW), Math.max(1, maxW));
        h = clamp(h, Math.max(1, g.minH), Math.max(1, g.maxH));

        if (dir.includes('w')) {
            x = clamp(g.startX + g.startW - w, 0, Math.max(0, columns - w));
        } else {
            x = clamp(x, 0, Math.max(0, columns - w));
        }

        if (dir.includes('n')) {
            y = Math.max(0, g.startY + g.startH - h);
        } else {
            y = Math.max(0, y);
        }

        return {
            x,
            y,
            w,
            h,
            px: {
                left: dir.includes('w') ? dx : 0,
                top: dir.includes('n') ? dy : 0,
                width: dir.includes('e') ? dx : (dir.includes('w') ? -dx : 0),
                height: dir.includes('s') ? dy : (dir.includes('n') ? -dy : 0),
            },
        };
    }

    handlePointerUp(e) {
        if (!this.gesture || e.pointerId !== this.gesture.pointerId) {
            return;
        }
        this.endGesture(true);
    }

    endGesture(notify) {
        const g = this.gesture;
        if (!g) {
            return;
        }

        this.gesture = null;
        if (this.frame) {
            cancelAnimationFrame(this.frame);
            this.frame = 0;
        }

        window.removeEventListener('pointermove', this.onPointerMove);
        window.removeEventListener('pointerup', this.onPointerUp);
        window.removeEventListener('pointercancel', this.onPointerUp);
        document.body.classList.remove('tc-gesture-active');

        const shell = this.itemElement(g.id)?.querySelector('.tc-item-shell');
        if (shell) {
            shell.style.transform = '';
            shell.style.width = '';
            shell.style.height = '';
        }

        if (notify && g.active) {
            this.dotNet.invokeMethodAsync('JsInteractionEnd', g.id);
        }
    }

    handleDragOver(e) {
        if (!this.options.acceptExternalDrops || this.gesture) {
            return;
        }
        e.preventDefault();
        if (e.dataTransfer) {
            e.dataTransfer.dropEffect = 'copy';
        }
        this.element.classList.add('tc-grid--drop-target');
    }

    handleDragLeave(e) {
        // dragleave also fires when crossing into a child element, so only clear the
        // highlight once the pointer has genuinely left the canvas.
        if (!this.element.contains(e.relatedTarget)) {
            this.element.classList.remove('tc-grid--drop-target');
        }
    }

    handleDrop(e) {
        if (!this.options.acceptExternalDrops) {
            return;
        }
        e.preventDefault();
        this.element.classList.remove('tc-grid--drop-target');

        const payload = e.dataTransfer ? e.dataTransfer.getData('text/plain') : '';
        const { left, top, stepX, stepY } = this.metrics();
        const x = clamp(Math.floor((e.clientX - left) / stepX), 0, this.options.columns - 1);
        const y = Math.max(0, Math.floor((e.clientY - top) / stepY));

        this.dotNet.invokeMethodAsync('JsExternalDrop', payload, x, y);
    }
}

function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}

export function init(element, dotNet, options) {
    if (!element) {
        return;
    }
    grids.get(element)?.detach();
    grids.set(element, new GridState(element, dotNet, options));
}

export function update(element, options) {
    const state = element && grids.get(element);
    if (state) {
        state.options = options;
    }
}

export function dispose(element) {
    const state = element && grids.get(element);
    if (state) {
        state.detach();
        grids.delete(element);
    }
}
