v8SignalRApp.controller('IndexCtrl', ['$scope', 'ScriptEngine', function ($scope, scriptEngine) {
    $scope.model = {
        userName: "Sean",
        message: "",
        code: "",
        result: "",
        messages: [],
        breakpoints: [],
        newBreakpointLineNum: null,
        currentBreakpoint: null
    };

    $scope.eval = function() {
        scriptEngine.eval($scope.model.userName, $scope.model.code);
    };

    $scope.setBreakpoint = function () {
        scriptEngine.setBreakpoint($scope.model.newBreakpointLineNum);
        $scope.model.newBreakpointLineNum = null;
    };

    $scope.continueBreakpoint = function () {
        scriptEngine.continueBreakpoint();
    };

    $scope.send = function () {
        scriptEngine.send($scope.model.userName, $scope.model.message);
        $scope.model.message = "";
    };

    $scope.$on("scriptEngineHub.addMessage", function(e, userName, message) {
        $scope.model.messages.push({ userName: userName, message: message });
    });

    $scope.$on("scriptEngineHub.breakpointHit", function (e, obj) {
        $scope.model.currentBreakpoint = obj;
    });

    $scope.$on("scriptEngineHub.breakpointSet", function (e, obj) {
        $scope.model.breakpoints.push(obj);
    });

    $scope.$on("scriptEngineHub.evalResult", function (e, userName, result) {
        $scope.model.result = JSON.stringify(result);
    });

}]);