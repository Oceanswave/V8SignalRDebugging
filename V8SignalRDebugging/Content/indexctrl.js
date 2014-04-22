v8SignalRApp.controller('IndexCtrl', ['$scope', 'ScriptEngine', function ($scope, scriptEngine) {
    $scope.model = {
        userName: "",
        message: "",
        messages: []
    };

    $scope.send = function () {
        scriptEngine.send($scope.model.userName, $scope.model.message);
        $scope.model.message = "";
    }

    $scope.$on("scriptEngineHub.addMessage", function(e, userName, message) {
        $scope.model.messages.push({ userName: userName, message: message });
    });

}]);