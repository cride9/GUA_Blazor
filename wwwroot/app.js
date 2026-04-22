window.autoResizeTextarea = (el) => {
    if (!el) return;
    el.style.height = "auto";
    el.style.height = el.scrollHeight + "px";
};

window.startSidebarResize = (sidebarEl, dotnetRef) => {
    const onMouseMove = (e) => {
        // sidebar is on the right, so width = viewport right edge - mouse X
        const newWidth = window.innerWidth - e.clientX;
        dotnetRef.invokeMethodAsync('OnSidebarResized', Math.round(newWidth));
    };
    const onMouseUp = () => {
        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        dotnetRef.invokeMethodAsync('OnSidebarResizeEnd');
    };
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';
};
