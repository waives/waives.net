﻿using System;

namespace Waives.Reactive
{
    public static class WaivesObservable
    {
        public static IDisposable SubscribeConsole<T>(this IObservable<T> observable, string name = "")
        {
            return observable.Subscribe(new ConsoleObserver<T>(name));
        }
    }
}