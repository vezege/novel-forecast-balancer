using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

Dictionary<int, List<string>> reviews = new Dictionary<int, List<string>>(5);
Dictionary<int, int> reviewsCount = new Dictionary<int, int>(5);
// the max number of reviews for each score
int limit = 28180;

// the max number of reviews for training
int trainLimit = 22180;

// count of words in all reviews (will be divided later)
double wordsCount = 0;

for (int i = 1; i <= 5; i++)
{
    reviews[i] = new List<string>(limit);
    reviewsCount[i] = 0;
}

/// parse each row of csv and collect a review from it (some reviews may be splited into several rows)
using (StreamReader sr = new StreamReader(AppContext.BaseDirectory + "/goodreads_train.csv"))
{
    string currentLine;
    string currentReview = string.Empty;
    while ((currentLine = sr.ReadLine()) != null)
    {
        if (currentLine.StartsWith("user_id"))
        {
            continue;
        }

        // if a row starts with user ID, book ID, and review ID, that means that a new review starts
        // in another case the row content is a next split of the current review
        if (IsStartReview(currentLine))
        {
            var processedReview = ProcessReview(currentReview, reviewsCount, limit);
            if (processedReview.Useful && processedReview.Rating > 0 && processedReview.Rating <= 5)
            {
                reviews[processedReview.Rating].Add(processedReview.Review);
                reviewsCount[processedReview.Rating]++;
                wordsCount = wordsCount + CountWords(processedReview.Review);
            }
            currentReview = currentLine;
        }
        else
        {
            if (currentReview.EndsWith(' '))
            {
                currentReview += currentLine.TrimStart();
            }
            else
            {
                currentReview += currentLine;
            }
        }

        // если строка новая, то обработаем старую строку,
        // т.е. уберем лишние символы типа кавычек двойных и спойлеров, сохраним отдельно текст отзыва и оценку
        // если есть ссылка внутри, то тоже убираем весь отзыв
    }

    foreach (var countPair in reviewsCount)
    {
        Console.WriteLine($"Рецензий с оценкой {countPair.Key}: {countPair.Value}");
    }

    Console.WriteLine($"Среднее количество слов: { Math.Ceiling(wordsCount / reviewsCount.Values.Sum()) }");
    Console.WriteLine("Приступаю к записи рецензий на диск:");

    for (int i = 0; i < limit; i++)
    {
        string subdirectory = i < trainLimit ? "Train" : "Test";
        int reviewNumber = i > trainLimit ? i - trainLimit : i + 1;
        for (int score = 1; score <= 5; score++)
        {
            string path = @$"C:\Users\VeZeG\Desktop\Пары\GoodreadsDataBalancer\Reviews\{subdirectory}\{score}\{reviewNumber}.txt";
            using (var writer = new StreamWriter(File.Open(path, FileMode.Create)))
            {
                writer.WriteLine(reviews[score][i]);
            }
        }
        Console.WriteLine($"Сохранение рецензий типа {subdirectory} завершено");
    }


    Console.WriteLine("Нажмите ENTER, чтобы продолжить:");
    Console.ReadLine();
}

static bool IsStartReview(string line)
{
    var index = line.IndexOf(',');

    if (index == -1)
    {
        return false;
    }

    var lineStart = line.Substring(0, index);

    return IsInnerId(lineStart);
}

static (bool Useful, string Review, int Rating) ProcessReview(string review, Dictionary<int, int> reviewCounts, int limit)
{
    // if a review contains a link, we delete it because it is likely a teaser of a full review located somewhere
    if (review.Contains("http"))
    {
        return (false, string.Empty, 0);
    }

    // try to obtain a review from a row full of extra data
    var commaSeparated = review.Split(',');

    if (IsInnerId(commaSeparated[0]) && int.TryParse(commaSeparated[1], out _) && IsInnerId(commaSeparated[2]))
    {
        if (int.TryParse(commaSeparated[3], out int score) && score > 0 && score <= 5)
        {
            if (reviewCounts[score] == limit)
            {
                return (false, string.Empty, 0);
            }
            return (true, GetReviewText(commaSeparated.Skip(4).ToArray()), score);
        }
    }

    return (false, string.Empty, 0);
}

static bool IsInnerId(string checkedString)
{
    Regex symbolsRegex = new Regex(@"^[a-z0-9]+$");

    return checkedString.Length == 32 && symbolsRegex.IsMatch(checkedString) && checkedString.Any(char.IsDigit) && checkedString.Any(char.IsLetter);
}

/// get a text of review filtered of spoiling symbols and collocations
static string GetReviewText(string[] stringParts)
{
    StringBuilder reviewText = new StringBuilder();

    foreach (string reviewPart in stringParts)
    {
        if (IsDate(reviewPart))
        {
            break;
        }

        reviewText.Append(reviewPart);
    }

    if (reviewText[0].Equals('"'))
    {
        reviewText.Remove(0, 1);
    }

    if (reviewText[reviewText.Length - 1].Equals('"'))
    {
        reviewText.Remove(reviewText.Length - 1, 1);
    }

    reviewText.Replace("\"\"", "\"");
    reviewText.Replace("\"\"\"", "\"");
    reviewText.Replace(" (hide spoiler)]", string.Empty);
    reviewText.Replace("(view spoiler)[", string.Empty);
    reviewText.Replace("** spoiler alert **", string.Empty);
    reviewText.Replace("[image error]", string.Empty);

    return reviewText.ToString();
}

static bool IsDate(string checkedString)
{
    return DateTime.TryParseExact(checkedString, "ddd MMM dd HH:mm:ss zzz yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}


static int CountWords(string reviewText)
{
    var wordsMatches = Regex.Matches(reviewText, @"\S+");
    return wordsMatches.Count;
}