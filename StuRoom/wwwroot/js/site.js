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
