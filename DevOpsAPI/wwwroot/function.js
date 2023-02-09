let JsFunctions = window.JsFunctions || {};

JsFunctions = {
    offset: function offset() {
        return new Date().getTimezoneOffset() / 60;
    }
};
