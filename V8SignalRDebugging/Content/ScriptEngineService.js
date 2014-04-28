v8SignalRApp.service('ScriptEngine', ['$rootScope', '$timeout', function ($rootScope, $timeout) {
    var scriptEngineHub = jQuery.connection.scriptEngineHub;

    scriptEngineHub.client.backtrace = function (backtrace) {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.backtrace", backtrace);
        });
    };

    scriptEngineHub.client.beginEval = function () {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.beginEval");
        });
    };

    scriptEngineHub.client.breakpointHit = function (breakpoint) {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.breakpointHit", breakpoint);
        });
    };

    scriptEngineHub.client.breakpointSet = function (breakpoint) {
        $timeout(function() {
            $rootScope.$broadcast("scriptEngineHub.breakpointSet", breakpoint);
        });
    };

    scriptEngineHub.client.breakpointContinue = function () {
        $timeout(function() {
            $rootScope.$broadcast("scriptEngineHub.breakpointContinue");
        });
    };

    scriptEngineHub.client.console = function (result) {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.console", result);
        });
    };

    scriptEngineHub.client.codeUpdated = function (code) {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.codeUpdated", code);
        });
    };

    scriptEngineHub.client.evalResult = function (result) {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.evalResult", result);
        });
    };

    scriptEngineHub.client.interrupt = function () {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.interrupt");
        });
    };

    scriptEngineHub.client.scope = function (result) {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.scope", result);
        });
    };

    scriptEngineHub.client.scopes = function (result) {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.scopes", result);
        });
    };

    return {
        backtrace: function () {
            scriptEngineHub.server.backtrace();
        },
        continueBreakpoint: function (stepAction, stepCount) {
            if (angular.isUndefined(stepCount))
                stepCount = 1;

            scriptEngineHub.server.continueBreakpoint(stepAction, stepCount);
        },
        disconnect: function() {
            scriptEngineHub.server.disconnect();
        },
        eval: function (code) {
            scriptEngineHub.server.eval(code);
        },
        evalImmediate: function (expression) {
            scriptEngineHub.server.evalImmediate(expression);
        },
        interrupt: function() {
            scriptEngineHub.server.interrupt();
        },
        scope: function (scopeNumber, frameNumber) {

            if (angular.isUndefined(frameNumber))
                frameNumber = null;

            scriptEngineHub.server.scope(scopeNumber, frameNumber);
        },
        scopes: function (frameNumber) {

            if (angular.isUndefined(frameNumber))
                frameNumber = null;

            scriptEngineHub.server.scopes(frameNumber);
        },
        shareCode: function (code) {
            scriptEngineHub.server.shareCode(code);
        },
        send: function (userName, message) {
            scriptEngineHub.server.send(userName, message);
        },
        setBreakpoint: function (lineNumber, column, enabled, condition, ignoreCount) {

            if (angular.isUndefined(column))
                column = null;

            if (angular.isUndefined(enabled))
                enabled = true;

            if (angular.isUndefined(condition))
                condition = null;

            if (angular.isUndefined(ignoreCount))
                ignoreCount = 0;

            scriptEngineHub.server.setBreakpoint(lineNumber, column, enabled, condition, ignoreCount);
        }
    };
}]);