import ReactPlayer from "react-player";
import SideBar from "./component/sidebar";

function App() {
  const videoList = [
    { title: "Video 1", url: "https://www.youtube.com/watch?v=g7rABOkk1cU" },
    { title: "Video 2", src: "/videos/video2.mp4" },
    { title: "Video 3", src: "/videos/video3.mp4" },
    { title: "Video 4", src: "/videos/video4.mp4" },
    { title: "Video 5", src: "/videos/video5.mp5" },
  ];

  return (
    <div className="flex flex-col h-screen">
      <div className="grow-0">
        <SideBar />
      </div>
      <div className="grow w-full flex flex-col gap-10">
        <div className="min-h-screen p-6">
          <h1 className="text-2xl font-bold mb-6">YouTube Video Gallery</h1>
          <div className="flex flex-wrap justify-center gap-10">
            {videoList.map((video, index) => (
              <div
                key={index}
                className="flex-shrink-0 w-full sm:w-[45%] lg:w-[40%] bg-white rounded-2xl shadow-lg p-4"
              >
                <h2 className="text-xl font-semibold mb-2">{video.title}</h2>
                <ReactPlayer
                  url={video.url}
                  width="100%"
                  height="200px"
                  controls
                />
              </div>
            ))}
          </div>
        </div>
        <div>
          <h1>media</h1>
        </div>
      </div>
    </div>
  );
}

export default App;
