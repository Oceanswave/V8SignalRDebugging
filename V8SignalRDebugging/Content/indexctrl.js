v8SignalRApp.controller('IndexCtrl', ['$scope', 'ScriptEngine', function ($scope, scriptEngine) {
    $scope.model = {
        userName: "Sean",
        message: "",
        code: "",
        result: "",
        messages: []
    };

    $scope.eval = function() {
        scriptEngine.eval($scope.model.userName, $scope.model.code);
    };

    $scope.send = function () {
        scriptEngine.send($scope.model.userName, $scope.model.message);
        $scope.model.message = "";
    };

    $scope.$on("scriptEngineHub.addMessage", function(e, userName, message) {
        $scope.model.messages.push({ userName: userName, message: message });
    });

    $scope.$on("scriptEngineHub.evalResult", function (e, userName, result) {
        $scope.model.result = JSON.stringify(result);
    });

}]);