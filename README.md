# FrameMachine
Fixed space GA engine. The origin of ML experiments for Arbitrary LLC.

## Motivation
Most GA engines are stack based, and evaluate FORTH-like postfix languages. Unless they are restricted in complexity, these systems require non-determinable space and/or time to evaluate an arbitrary program. Figuring out what parameters to restrict becomes a higher-level problem in itself. I always recall a researcher saying something along the lines of: "The only real problem with [our GA system] is halting."

The efficiency of GPGPU hardware captivates modern ML, and perhaps the success of ANN and statistical techniques forms a commutual relationship focused on producing this hardware. In general, the dynamic space requirements of GA software precludes convenient use on GPGPU hardware due to its inherent constraints on complexity. I sought to design an algorithm that could efficiently operate on GPGPU hardware.

## Concept
Each candidate consists of a code buffer and two or more data buffers, all the same fixed size. These buffers are referred to as 'frames'. (In practice, more than two data frames are unnecessary.)

For each evaluation cycle, a successive data frame is selected for output, in a cyclic manner. The instructions in the code frame address two values from the preceding data frame, an operation is performed with the two values, and the result is written to the data frame at the same relative index of the instruction in the code frame.

Each candidate is cycled a fixed number of times proportional to each input, and then evaluated for fitness with a regular method of conventional GAs. Unfit candidates are culled and replaced with a new generation, and the process repeats until the desired result is obtained.

## Results
At first, the system was tested using input from generic functions. (Some of which are included in the original source here.) For example, a sine function would be input, and it was desired that the system detect the phase. Once the behavior of the system was better understood, arbitrarily more complex tasks were concocted.
### Market Trading
Live market tick data of a selection of equity ETFs was fed into the system, along with metadata associated with each candidate. (Metadata such as account balance, current holdings, etc.) Output from candidates was interpreted as either opening, closing, or holding a position in some ETF. Candidate metadata was adjusted based on the output, and evaluated for fitness, that is: the value of the associated balance over time.

Several runs with manually adjusted initial parameters were performed on a mid-range server platform over about four months, with the longest run taking just over three weeks. Runs were halted when the fittest candidates persisted over two days.

The code and states of the fittest candidates were manually analyzed. The fittest candidate from the three-week run recorded a profit 10.3%. I interpreted the fittest candidate's evaluation to be a form of segmented regression. I used this method to achieve 33.7% profit on a brokerage account over 2018. Notably, 'breakers' that I had implemented prevented the strategy from making any trades for most of February, and the last three months of 2018, when it had been trading at a loss.

The best insight I received from these experiments was to perform statistical analysis on tick data instead of data segmented by time span, for short-term analysis. The scale of volume with regard to price is otherwise obscured.
### Tweet Sentiment
The unaltered message contents of tweets mentioning certain large-cap or contentious companies like AAPL or TSLA were input into each candidate. Candidate output was interpreted as binary good or bad sentiment. Fitness was judged by comparing the output to binary gain or loss of the associated company's stock price over some number of ticks. Runs were halted when the fittest candidates persisted over two days.

Many runs were performed over two months, with the same hardware as above. None ran over four days. No candidates achieved a material level of fitness.

## Problems
I believe the system reduces into simple regression of differing parameters. I don't think it is suitable to regard it as a GA at all. The decisions made by the candidates in the trading task appeared to differ depending only on the length of samples taken, and z-score thresholds. This may explain why the system seemed to perform poorly when given the more qualitative input from the tweet sentiment task.

Manually tuning the initial parameters was also tricky. There was a small window for chaos, otherwise the output data would be zeroed/oned out, or simply copies of the input.

The algorithm was never implemented for GPGPU. As a result, my focus with Arbitrary pivoted towards researching methods of grammar induction and normalized semantic hashing.
