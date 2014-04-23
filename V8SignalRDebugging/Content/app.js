var v8SignalRApp = angular.module('v8-signalr', [])
.run(function() {
        jQuery.connection.hub.url = "http://localhost:8080/signalr";

        // Start the connection.
        jQuery.connection.hub.start();
    })

.service('ScriptEngine', ['$rootScope', '$timeout', function ($rootScope, $timeout) {
    var scriptEngineHub = jQuery.connection.scriptEngineHub;

    // Create a function that the hub can call to broadcast messages.
    scriptEngineHub.client.addMessage = function (name, message) {
        $timeout(function() {
            $rootScope.$broadcast("scriptEngineHub.addMessage", name, message);
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

    return {
        continueBreakpoint: function(stepAction, stepCount) {
            scriptEngineHub.server.continueBreakpoint("Next", 1);
        },
        setBreakpoint: function(lineNumber, column, enabled, condition, ignoreCount) {
            scriptEngineHub.server.setBreakpoint(lineNumber);
        },
        eval: function (userName, code) {
            scriptEngineHub.server.eval(userName, code);
        },
        send: function (userName, message) {
            scriptEngineHub.server.send(userName, message);
        }
    };
}]);