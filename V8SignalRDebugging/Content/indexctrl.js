v8SignalRApp.controller('IndexCtrl', ['$scope', 'ScriptEngine', function ($scope, scriptEngine) {
    $scope.model = {
        userName: "Sean",
        message: "",
        code: "",
        result: "",
        isRunning: false,
        messages: [],
        breakpoints: [],
        newBreakpointLineNum: null,
        currentBreakpoint: null,
        evalImmediateExpression: null,
        consoleMessages: []
    };

    $scope.backtrace = function() {
        scriptEngine.backtrace();
    };

    $scope.codeUpdated = function() {
        scriptEngine.shareCode($scope.model.code);
    };

    $scope.disconnect = function () {
        scriptEngine.disconnect();
    };

    $scope.eval = function () {
        store.set("code", $scope.model.code);
        $scope.model.isRunning = true;
        $scope.model.result = null;
        $scope.model.evalImmediateExpression = null;
        $scope.model.consoleMessages = [];
        scriptEngine.eval($scope.model.userName, $scope.model.code);
    };

    $scope.evalImmediate = function () {
        if ($scope.model.isRunning == false)
            return;

        scriptEngine.evalImmediate($scope.model.evalImmediateExpression);
        $scope.model.evalImmediate = null;
    };


    $scope.setBreakpoint = function () {
        scriptEngine.setBreakpoint($scope.model.newBreakpointLineNum);
        $scope.model.newBreakpointLineNum = null;
    };

    $scope.continueBreakpoint = function (type) {
        scriptEngine.continueBreakpoint(type);
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
        $scope.model.result = JSON.stringify(result, null, 4);
        $scope.model.isRunning = false;
    });

    $scope.$on("scriptEngineHub.console", function (e, result) {
        if (result.success == true) {
            if (result.body.type == "object") {
                var stringResult = JSON.stringify(result.body, null, 4);
                $scope.model.consoleMessages.push({ date: new Date(), message: stringResult });
            } else {
                $scope.model.consoleMessages.push({ date: new Date(), message: result.body.text });
            }
        }
        else {
            $scope.model.consoleMessages.push({ date: new Date(), message: result.message });
        }
        
    });

    $scope.$on("scriptEngineHub.codeUpdated", function (e, code) {
        $scope.model.code = code;
    });

    $scope.$on("scriptEngineHub.backtrace", function (e, backtrace) {
        $scope.model.backtrace = backtrace;
    });

    $scope.model.code = store.get("code");
}]);