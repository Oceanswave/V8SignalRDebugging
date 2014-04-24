var v8SignalRApp = angular.module('v8-signalr', ['ui.bootstrap'])
.run(function() {
        jQuery.connection.hub.url = "http://localhost:8080/signalr";

        // Start the connection.
        jQuery.connection.hub.start();
    })
