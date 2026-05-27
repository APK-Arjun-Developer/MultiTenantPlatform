(function () {
    const bearerScheme = "Bearer";
    const storageKeys = {
        refreshToken: "swagger_refresh_token",
        accessToken: "swagger_access_token",
    };

    const tokenResponsePaths = ["/auth/login", "/auth/register", "/auth/refresh"];
    const refreshTokenRequestPaths = ["/auth/refresh", "/auth/logout"];

    function getUrl(args) {
        const target = args[0];
        return (typeof target === "string" ? target : (target && target.url) || "").toLowerCase();
    }

    function getTokens(body) {
        if (!body) {
            return null;
        }

        return {
            accessToken: body.accessToken || body.AccessToken,
            refreshToken: body.refreshToken || body.RefreshToken,
        };
    }

    function storeTokens(tokens) {
        if (tokens.refreshToken) {
            localStorage.setItem(storageKeys.refreshToken, tokens.refreshToken);
        }

        if (tokens.accessToken) {
            localStorage.setItem(storageKeys.accessToken, tokens.accessToken);
        }
    }

    function clearTokens() {
        localStorage.removeItem(storageKeys.refreshToken);
        localStorage.removeItem(storageKeys.accessToken);
    }

    function applyBearer(token) {
        if (!token || !window.ui || !window.ui.preauthorizeApiKey) {
            return;
        }

        window.ui.preauthorizeApiKey(bearerScheme, token);
    }

    function clearBearer() {
        if (!window.ui) {
            return;
        }

        if (window.ui.authActions && window.ui.authActions.logout) {
            window.ui.authActions.logout();
            return;
        }

        if (window.ui.preauthorizeApiKey) {
            window.ui.preauthorizeApiKey(bearerScheme, "");
        }
    }

    function handleAuthResponse(url, response) {
        if (!response.ok) {
            return;
        }

        if (url.includes("/auth/logout")) {
            clearTokens();
            clearBearer();
            return;
        }

        if (!tokenResponsePaths.some(function (path) { return url.includes(path); })) {
            return;
        }

        response.clone().json().then(function (body) {
            const tokens = getTokens(body);
            if (!tokens) {
                return;
            }

            storeTokens(tokens);
            applyBearer(tokens.accessToken);
        }).catch(function () { });
    }

    function patchRefreshTokenRequest(args) {
        const url = getUrl(args);
        if (!refreshTokenRequestPaths.some(function (path) { return url.includes(path); })) {
            return args;
        }

        const stored = localStorage.getItem(storageKeys.refreshToken);
        if (!stored) {
            return args;
        }

        const init = args[1];
        if (!init || !init.body || typeof init.body !== "string") {
            return args;
        }

        try {
            const body = JSON.parse(init.body);
            if (!body.refreshToken && !body.RefreshToken) {
                body.refreshToken = stored;
                args[1] = Object.assign({}, init, { body: JSON.stringify(body) });
            }
        } catch (e) { }

        return args;
    }

    const originalFetch = window.fetch;
    window.fetch = function () {
        const args = Array.prototype.slice.call(arguments);
        patchRefreshTokenRequest(args);

        return originalFetch.apply(this, args).then(function (response) {
            handleAuthResponse(getUrl(args), response);
            return response;
        });
    };
})();
