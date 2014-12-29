function FirstJSClass() {

}

Object.defineProperties(FirstJSClass.prototype, {
    Message: {
        get: function () {
            return "Hello, " + this.Name + "!";
        },
        enumerable: true,
        configurable: true
    },
    GetMessage: {
        value: function () { return this.Message; },
        enumerable: true,
        configurable: true
    }
});

FirstJSClass.prototype.Name = "username";

registerClass(FirstJSClass);