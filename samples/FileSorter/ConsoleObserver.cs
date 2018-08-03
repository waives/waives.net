﻿using System;

namespace FileSorter
{
    public class ConsoleObserver<T> : IObserver<T>
    {
        private readonly string _name;

        public ConsoleObserver(string name = "")
        {
            _name = name;
        }

        public void OnCompleted()
        {
            Console.WriteLine($"{_name} - OnCompleted()");
        }

        public void OnError(Exception error)
        {
            Console.WriteLine($"{_name} - OnError:");
            Console.WriteLine($"\t{error}");
        }

        public void OnNext(T value)
        {
            Console.WriteLine($"{_name} - OnNext({value})");
        }
    }

    public static class Extensions
    {
        public static IDisposable SubscribeConsole<T>(this IObservable<T> observable, string name = "")
        {
            return observable.Subscribe(new ConsoleObserver<T>(name));
        }
    }
}