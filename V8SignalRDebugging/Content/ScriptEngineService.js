v8SignalRApp.service('ScriptEngine', ['$rootScope', '$timeout', function ($rootScope, $timeout) {
    var scriptEngineHub = jQuery.connection.scriptEngineHub;

    // Create a function that the hub can call to broadcast messages.
    scriptEngineHub.client.addMessage = function (name, message) {
        $timeout(function() {
            $rootScope.$broadcast("scriptEngineHub.addMessage", name, message);
        });
    };

    scriptEngineHub.client.backtrace = function (backtrace) {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.backtrace", backtrace);
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

    scriptEngineHub.client.evalResult = function (name, result) {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.evalResult", name, result);
        });
    };

    scriptEngineHub.client.console = function (name, result) {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.console", name, result);
        });
    };

    scriptEngineHub.client.codeUpdated = function (code) {
        $timeout(function () {
            $rootScope.$broadcast("scriptEngineHub.codeUpdated", code);
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
        eval: function (userName, code) {
            scriptEngineHub.server.eval(userName, code);
        },
        evalImmediate: function (expression) {
            scriptEngineHub.server.evalImmediate(expression);
        },
        shareCode: function (code) {
            scriptEngineHub.server.shareCode(code);
        },
        send: function (userName, message) {
            scriptEngineHub.server.send(userName, message);
        },
        setBreakpoint: function(lineNumber, column, enabled, condition, ignoreCount) {
            scriptEngineHub.server.setBreakpoint(lineNumber);
        }
    };
}]);