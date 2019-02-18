# difficulty-analyzer 
**osu!std only**, show a map's aim & speed distribution  
copy-paste based coding, useless, and may not accurate
  
*click timestamp below the chart can locate the time point in osu editor*  

> overall data is just `aim + speed`
  
> filter applies ` data * Math.Pow(0.9, n))` to each point
> while `n` is its position after sorting (big to small)