let lang = {};
let currentLanguage = localStorage.getItem("jlmsLanguage") || "ta";
let languageChangeCallbacks = [];

function registerLanguageCallback(callback) {
    languageChangeCallbacks.push(callback);
}

async function loadLanguage(language = currentLanguage) {
    currentLanguage = language;
    localStorage.setItem("jlmsLanguage", language);

    try {
        const response = await fetch("../pages/lang/" + language + ".json");
        if (!response.ok) {
            console.error("Failed to load language file:", response.status);
            return;
        }
        lang = await response.json();
        console.log("Language loaded:", language, "with", Object.keys(lang).length, "keys");

        const i18nElements = document.querySelectorAll("[data-i18n]");
        console.log("Found", i18nElements.length, "elements with data-i18n attribute");
        
        i18nElements.forEach(el => {
            const key = el.getAttribute("data-i18n");
            console.log("Processing key:", key, "value:", lang[key]);
            if (lang[key]) {
                el.innerText = lang[key];
            }
        });

        const placeholderElements = document.querySelectorAll("[data-i18n-placeholder]");
        console.log("Found", placeholderElements.length, "elements with data-i18n-placeholder attribute");
        
        placeholderElements.forEach(el => {
            const key = el.getAttribute("data-i18n-placeholder");
            if (lang[key]) {
                el.placeholder = lang[key];
            }
        });

        // Call registered callbacks to update dynamic content
        languageChangeCallbacks.forEach(callback => {
            try {
                callback();
            } catch (err) {
                console.error("Error in language callback:", err);
            }
        });
        return true;
    } catch (error) {
        console.error("Error loading language:", error);
        return false;
    }
}

function switchLanguage(language) {
    console.log("Switching to language:", language);
    loadLanguage(language);
}

window.switchLanguage = switchLanguage;

// Load language immediately when script runs
loadLanguage(currentLanguage);

// Also load on DOMContentLoaded as backup
document.addEventListener('DOMContentLoaded', () => {
    console.log("DOMContentLoaded fired, loading language:", currentLanguage);
    loadLanguage(currentLanguage);
});