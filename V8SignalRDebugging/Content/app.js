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

    return {
        send: function (userName, message) {
            scriptEngineHub.server.send(userName, message);
        }
    }
}]);