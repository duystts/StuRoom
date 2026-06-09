// StuRoom — Theme Toggle & Site Interactions
document.addEventListener("DOMContentLoaded", () => {
    const themeToggleBtn = document.getElementById("themeToggle");
    if (themeToggleBtn) {
        const icon = themeToggleBtn.querySelector("i");
        
        // Function to update the toggle icon based on theme
        const updateIcon = (theme) => {
            if (theme === "dark") {
                icon.className = "bi bi-sun fs-5";
            } else {
                icon.className = "bi bi-moon fs-5";
            }
        };

        // Initialize toggle button icon
        const currentTheme = document.documentElement.getAttribute("data-theme") || "light";
        updateIcon(currentTheme);

        // Toggle click handler
        themeToggleBtn.addEventListener("click", () => {
            const newTheme = document.documentElement.getAttribute("data-theme") === "dark" ? "light" : "dark";
            
            // Apply theme and save
            document.documentElement.setAttribute("data-theme", newTheme);
            localStorage.setItem("theme", newTheme);
            
            // Update icon representation
            updateIcon(newTheme);

            // Broadcast theme change event so active charts can adapt
            const event = new CustomEvent("theme-changed", { detail: { theme: newTheme } });
            window.dispatchEvent(event);
        });
    }
});

// Global Toast Notification System
window.showToast = function (message, type = 'success') {
    let container = document.getElementById('toastContainer');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toastContainer';
        container.className = 'toast-container position-fixed top-0 end-0 p-3';
        container.style.zIndex = '99999';
        container.style.marginTop = '70px'; // clear navbar height
        document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.className = 'sturoom-toast d-flex align-items-center p-3 mb-2 border-0 rounded-4 shadow-sm show';
    toast.role = 'alert';
    toast.ariaLive = 'assertive';
    toast.ariaAtomic = 'true';

    // Theme and icon styling
    let iconClass = 'bi-check-circle-fill text-success';
    let typeClass = 'toast-success';
    if (type === 'error') {
        iconClass = 'bi-exclamation-circle-fill text-danger';
        typeClass = 'toast-error';
    } else if (type === 'info') {
        iconClass = 'bi-info-circle-fill text-primary';
        typeClass = 'toast-info';
    } else if (type === 'warning') {
        iconClass = 'bi-exclamation-triangle-fill text-warning';
        typeClass = 'toast-warning';
    }

    toast.classList.add(typeClass);

    toast.innerHTML = `
        <div class="toast-icon me-2.5 d-flex align-items-center">
            <i class="bi ${iconClass} fs-5"></i>
        </div>
        <div class="toast-body small fw-semibold flex-grow-1 text-dark text-start">
            ${message}
        </div>
        <button type="button" class="btn-close ms-2.5 small" style="font-size: 10px;" onclick="this.parentElement.remove()"></button>
    `;

    container.appendChild(toast);

    // Auto-dismiss after 4 seconds
    setTimeout(() => {
        toast.classList.add('hide');
        setTimeout(() => {
            toast.remove();
        }, 300);
    }, 4000);
};

