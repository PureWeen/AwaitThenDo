using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Reactive.Concurrency;
using System.Reactive.Threading.Tasks;
using System.Reactive.Disposables;

namespace AwaitThenDo.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<string> text = new List<string>();
        
        public MainWindow()
        {
            InitializeComponent();

            Observable.Merge(
                SetupWithObserveOnDispatcher(),
                SetupWithToObservableCurrentThread(),
                SetupWithNoObserveOns()
            )
            .ObserveOnDispatcher()
            .Subscribe(_ => UpdateDisplay());
        }

        IObservable<Unit> SetupWithNoObserveOns()
        {
            var runAsAlreadyCompletedTask =
                Observable.FromEventPattern<RoutedEventHandler, RoutedEventArgs>(
                  x => btnTriggerCompleted.Click += x,
                  x => btnTriggerCompleted.Click -= x)
                  .Select(_ => true);

            var runAsNotCompletedTask =
                Observable.FromEventPattern<RoutedEventHandler, RoutedEventArgs>(
                  x => btnTriggerNotCompleted.Click += x,
                  x => btnTriggerNotCompleted.Click -= x)
                  .Select(_ => false);

            return runAsAlreadyCompletedTask
                .Merge(runAsNotCompletedTask)
                .SelectMany(async alreadyCompletedTask =>
                {
                    //Here we will be on the dispatcher thread
                    await GetTask(alreadyCompletedTask);
                    //Here we will still be on the dispatcher thread
                    WriteCurrentThread("After Await");
                    return Unit.Default;
                    //Once we leave this block all bets are off what thread we will be on
                })
                .Select(_ =>
                {
                    /*
                     * If the Task was already completed when checked by Rx then we will]
                     * still be on the dispatcher.
                     * https://github.com/Reactive-Extensions/Rx.NET/blob/master/Rx.NET/Source/System.Reactive.Linq/Reactive/Threading/Tasks/TaskObservableExtensions.cs#L149
                     * 
                     * Otherwise you will be at the mercy of ContinueWith 
                     * http://blog.stephencleary.com/2013/10/continuewith-is-dangerous-too.html. 
                     * 
                     * I first came across this with code that worked fine on Android 
                     * but crashed iOS due to how the timing on the Task worked on one 
                     * platform vs the other. 
                     * 
                     * This can just be unexpected as you are still on the UI Thread 
                     * after the await and there are some conditions that will cause 
                     * this block to run on the UI Thread and everything will seem fine. 
                     * Typically we think of await semantics as a way to be safe about 
                     * staying on the UI Thread without having to worry about how we 
                     * schedule the continuation ourselves. 
                     */
                    WriteCurrentThread("Inside Next Observable Block");
                    return Unit.Default;
                });
        }


        IObservable<Unit> SetupWithObserveOnDispatcher()
        {
            return 
                Observable.FromEventPattern<RoutedEventHandler, RoutedEventArgs>(
                    x => btnTriggerNotCompletedDispatcher.Click += x,
                    x => btnTriggerNotCompletedDispatcher.Click -= x
                )
                .Select(_ => false)
                .SelectMany(async alreadyCompletedTask =>
                {
                    await GetTask(alreadyCompletedTask);
                    WriteCurrentThread("After Await");
                    return Unit.Default;
                })
                //Just using normal ObserveOnDispatcher
                .ObserveOnDispatcher()
                .Select(_ =>
                {
                    WriteCurrentThread("Inside Next Observable Block");
                    return Unit.Default;
                }) ;
        }


        IObservable<Unit> SetupWithToObservableCurrentThread()
        {
            return 
                Observable.FromEventPattern<RoutedEventHandler, RoutedEventArgs>
                (
                  x => btnTriggerNotCompletedUIExt.Click += x,
                  x => btnTriggerNotCompletedUIExt.Click -= x
                )
                .Select(_ => false)
                .Select(async alreadyCompletedTask =>
                {
                    await GetTask(alreadyCompletedTask);
                    WriteCurrentThread("After Await"); 
                })
                //Using an extension to a write a little less code when converting TPL to Observables
                .SelectMany(TaskFromUiThread => TaskFromUiThread.ToObservableCurrentThread())
                .Select(_ =>
                {
                    WriteCurrentThread("Inside Next Observable Block");
                    return Unit.Default;
                });
        }

        Task GetTask(bool alreadyCompletedTask, [CallerMemberName]string method = "")
        {
            if(alreadyCompletedTask)
            {
                WriteCurrentThread($"Running Already Completed Task: {method}");
                return Task.Delay(0);
            }
            else
            {
                WriteCurrentThread($"Running Not Completed Task: {method}");
                return Task.Delay(1);
            }
        }

        void WriteCurrentThread(string desc)
        {
            text.Add($"ThreadId: {Thread.CurrentThread.ManagedThreadId}- {desc} "); 
        }

        void UpdateDisplay()
        {
            StringBuilder sb = new StringBuilder();
            text.ForEach(t => sb.AppendLine(t));
            tbThreads.Text = sb.ToString();
            text.Clear();
        }
    }



    public static class Extensions
    {
        /// <summary>
        /// I find this to be useful in cases where I am interfacing with a TPL based
        /// library i.e. Xamarin.Forms In Xamarin Forms all the Navigation points 
        /// are exposed as Task so I use this to expose them 
        /// 
        /// NavigationPage.PopAsync().ToObservableCurrentThread() 
        /// 
        /// So that the continuation will remain on the dispatcher thread
        /// </summary>
        /// <param name="This"></param>
        /// <returns></returns>
        public static IObservable<Unit> ToObservableCurrentThread(this Task This)
        {
            //initially I'd used a Subject but that caused a deadlock
            //This ensures the task will complete before moving on down the Observable
            AsyncSubject<Unit> returnValue = new AsyncSubject<Unit>();

            Observable.StartAsync(async () =>
            {
                try
                {
                    if (!This.IsCompleted)
                    {
                        await This;
                    }

                    returnValue.OnNext(Unit.Default);
                    returnValue.OnCompleted();
                }
                catch (Exception exc)
                {
                    returnValue.OnError(exc);
                }

            });

            return returnValue;
        }
    }

}
