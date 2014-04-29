v8SignalRApp.controller('IndexCtrl', ['$scope', 'ScriptEngine', function ($scope, scriptEngine) {
    $scope.model = {
        userName: "Sean",
        code: "",
        result: "",
        isRunning: false,
        breakpoints: [],
        newBreakpointLineNum: null,
        currentBreakpoint: null,
        evalImmediateExpression: null,
        consoleMessages: []
    };

    $scope.beginEval = function() {
        $scope.model.isRunning = true;
        $scope.model.result = null;
        $scope.model.evalImmediateExpression = null;
        $scope.model.consoleMessages = [];
    };

    $scope.backtrace = function() {
        scriptEngine.backtrace();
    };

    $scope.codeUpdated = function() {
        scriptEngine.shareCode($scope.model.code);
    };

    $scope.continueBreakpoint = function (type) {
        scriptEngine.continueBreakpoint(type);
    };

    $scope.disconnect = function () {
        scriptEngine.disconnect();
    };

    $scope.eval = function () {
        store.set("code", $scope.model.code);
        scriptEngine.eval($scope.model.code);
    };

    $scope.evalImmediate = function () {
        if ($scope.model.isRunning == false)
            return;

        scriptEngine.evalImmediate($scope.model.evalImmediateExpression);
        $scope.model.evalImmediate = null;
    };

    $scope.interrupt = function() {
        scriptEngine.interrupt();
    };

    $scope.getScopeVariables = function () {
        scriptEngine.getScopeVariables();
    };

    $scope.getFormattedObject = function(obj) {
        if (obj.type == "object") {
            var stringResult = JSON.stringify(obj.value, null, 4);
            return stringResult;
        } else {
            return obj.text;
        }
    };

    $scope.setBreakpoint = function () {
        scriptEngine.setBreakpoint($scope.model.newBreakpointLineNum);
        $scope.model.newBreakpointLineNum = null;
    };

    $scope.$on("scriptEngineHub.backtrace", function (e, backtrace) {
        $scope.model.backtrace = backtrace;
    });

    $scope.$on("scriptEngineHub.beginEval", function (e) {
        $scope.beginEval();
    });

    $scope.$on("scriptEngineHub.breakpointHit", function (e, obj) {
        $scope.model.currentBreakpoint = obj;
        $scope.getScopeVariables();
    });

    $scope.$on("scriptEngineHub.breakpointSet", function (e, obj) {
        $scope.model.breakpoints.push(obj);
    });

    $scope.$on("scriptEngineHub.codeUpdated", function (e, code) {
        $scope.model.code = code;
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

    $scope.$on("scriptEngineHub.evalResult", function (e, result) {
        $scope.model.result = JSON.stringify(result, null, 4);
        $scope.model.isRunning = false;
    });

    $scope.$on("scriptEngineHub.interrupt", function (e, result) {
        $scope.model.result = "*** Stopped before a result could be returned ***";
        $scope.model.isRunning = false;
    });

    $scope.$on("scriptEngineHub.scopeVariables", function (e, scopeNumber, frameNumber, scopeVariables) {
        $scope.model.scopeVariables = scopeVariables;
    });

    $scope.model.code = store.get("code");
}]);