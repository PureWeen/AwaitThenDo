using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System; 
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Concurrency;
using System.Threading;

namespace AwaitThenDo.Benchmark
{
    public class Md5VsSha256
    {

        public Md5VsSha256()
        {
        }


        [Benchmark, System.STAThread]
        public Task DelayOfOne() => PerformWork(1);

        [Benchmark, System.STAThread]
        public Task NoDelay() => PerformWork(0);
        [Benchmark, System.STAThread]
        public Task DelayOfOneScheduler() => PerformWorkWithScheduler(1);

        [Benchmark, System.STAThread]
        public Task NoDelayScheduler() => PerformWorkWithScheduler(0);


        public async Task PerformWork(int delay)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                throw new ThreadStateException("The current threads apartment state is not STA");
            }

            await Observable.Return(Unit.Default)
               .SelectMany(async _ =>
               { 
                   await Task.Delay(1);
                   return Unit.Default;
               })
               .Do(_ =>
               {
                   //new NewThreadScheduler()
               }
               )
               .Take(1);
        }

        public async Task PerformWorkWithScheduler(int delay)
        {
            await Observable.Return(Unit.Default)
               .SelectMany(async _ =>
               {
                   await Task.Delay(1);
                   return Unit.Default;
               }) 
               .Do(_ =>
               {
               }
               )
               .Take(1);
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<Md5VsSha256>();
        }
    }
}