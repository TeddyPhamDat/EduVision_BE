"use client"

import { useState } from "react"
import {
  Play,
  ChevronDown,
  Wand2,
  BookOpen,
  Users,
  Clock,
  Star,
  ArrowRight,
  CheckCircle,
  GraduationCap,
  FileText,
  Presentation,
} from "lucide-react"

const HomePage = () => {
  const [selectedKhoi, setSelectedKhoi] = useState("")
  const [selectedChuong, setSelectedChuong] = useState("")
  const [selectedSlideType, setSelectedSlideType] = useState("")
  const [isGenerating, setIsGenerating] = useState(false)

  // Dữ liệu khối lớp
  const khoiOptions = [
    { value: "tieu-hoc", label: "Tiểu học (Lớp 1-5)" },
    { value: "thcs", label: "THCS (Lớp 6-9)" },
    { value: "thpt", label: "THPT (Lớp 10-12)" },
    { value: "dai-hoc", label: "Đại học" },
  ]

  // Dữ liệu chương theo khối
  const chuongOptions = {
    "tieu-hoc": [
      { value: "toan-1", label: "Toán lớp 1 - Số và phép tính" },
      { value: "toan-2", label: "Toán lớp 2 - Phép cộng trừ" },
      { value: "tieng-viet-1", label: "Tiếng Việt lớp 1 - Học chữ cái" },
      { value: "khtn-3", label: "KHTN lớp 3 - Thế giới xung quanh" },
    ],
    thcs: [
      { value: "toan-6", label: "Toán 6 - Số tự nhiên" },
      { value: "toan-7", label: "Toán 7 - Số hữu tỉ" },
      { value: "ly-8", label: "Vật lý 8 - Ánh sáng" },
      { value: "hoa-9", label: "Hóa học 9 - Phi kim" },
      { value: "sinh-6", label: "Sinh học 6 - Tế bào" },
    ],
    thpt: [
      { value: "toan-10", label: "Toán 10 - Hàm số" },
      { value: "ly-11", label: "Vật lý 11 - Điện học" },
      { value: "hoa-12", label: "Hóa học 12 - Hóa hữu cơ" },
      { value: "sinh-10", label: "Sinh học 10 - Di truyền" },
      { value: "su-11", label: "Lịch sử 11 - Cận đại" },
    ],
    "dai-hoc": [
      { value: "toan-cao-cap", label: "Toán cao cấp - Giải tích" },
      { value: "lap-trinh", label: "Lập trình - Cơ sở dữ liệu" },
      { value: "kinh-te", label: "Kinh tế - Vi mô" },
      { value: "ngoai-ngu", label: "Ngoại ngữ - IELTS" },
    ],
  }

  // Các kiểu slide
  const slideTypes = [
    {
      value: "co-ban",
      label: "Slide cơ bản",
      description: "Văn bản + hình ảnh đơn giản",
      icon: <FileText className="w-6 h-6" />,
    },
    {
      value: "tuong-tac",
      label: "Slide tương tác",
      description: "Có câu hỏi và bài tập",
      icon: <Users className="w-6 h-6" />,
    },
    {
      value: "hoat-hinh",
      label: "Slide hoạt hình",
      description: "Có animation và hiệu ứng",
      icon: <Play className="w-6 h-6" />,
    },
    {
      value: "thuc-hanh",
      label: "Slide thực hành",
      description: "Bài tập và ví dụ minh họa",
      icon: <Presentation className="w-6 h-6" />,
    },
  ]

  // Ví dụ các video đã tạo
  const exampleVideos = [
    {
      id: 1,
      title: "Toán 6 - Số tự nhiên",
      khoi: "THCS",
      chuong: "Chương 1",
      slideType: "Slide tương tác",
      duration: "8:30",
      thumbnail: "/placeholder.svg?height=180&width=320",
      views: "2.1k",
      rating: 4.8,
    },
    {
      id: 2,
      title: "Vật lý 11 - Điện học",
      khoi: "THPT",
      chuong: "Chương 3",
      slideType: "Slide hoạt hình",
      duration: "12:45",
      thumbnail: "/placeholder.svg?height=180&width=320",
      views: "1.8k",
      rating: 4.9,
    },
    {
      id: 3,
      title: "Tiếng Việt 1 - Học chữ cái",
      khoi: "Tiểu học",
      chuong: "Chương 1",
      slideType: "Slide hoạt hình",
      duration: "6:20",
      thumbnail: "/placeholder.svg?height=180&width=320",
      views: "3.2k",
      rating: 4.7,
    },
    {
      id: 4,
      title: "Hóa học 12 - Hóa hữu cơ",
      khoi: "THPT",
      chuong: "Chương 5",
      slideType: "Slide thực hành",
      duration: "15:15",
      thumbnail: "/placeholder.svg?height=180&width=320",
      views: "1.5k",
      rating: 4.6,
    },
  ]

  // Các tính năng nổi bật
  const features = [
    {
      icon: <GraduationCap className="w-8 h-8" />,
      title: "Theo chương trình chuẩn",
      description: "Nội dung được thiết kế theo chương trình giáo dục của Bộ GD&ĐT",
    },
    {
      icon: <Presentation className="w-8 h-8" />,
      title: "Đa dạng kiểu slide",
      description: "Từ slide cơ bản đến tương tác, hoạt hình phù hợp với từng bài học",
    },
    {
      icon: <Clock className="w-8 h-8" />,
      title: "Tạo nhanh chóng",
      description: "Chỉ cần chọn khối, chương và kiểu slide, video sẽ được tạo tự động",
    },
    {
      icon: <Users className="w-8 h-8" />,
      title: "Dành cho giáo viên",
      description: "Công cụ hoàn hảo giúp giáo viên chuẩn bị bài giảng hiệu quả",
    },
  ]

  // Các bước thực hiện
  const steps = [
    {
      step: 1,
      title: "Chọn khối lớp",
      description: "Chọn cấp học phù hợp từ tiểu học đến đại học",
    },
    {
      step: 2,
      title: "Chọn chương học",
      description: "Chọn chương cụ thể trong chương trình học",
    },
    {
      step: 3,
      title: "Chọn kiểu slide",
      description: "Chọn loại slide phù hợp với nội dung bài học",
    },
    {
      step: 4,
      title: "Tạo video",
      description: "AI sẽ tự động tạo video bài giảng hoàn chỉnh",
    },
  ]

  const handleGenerate = () => {
    if (!selectedKhoi || !selectedChuong || !selectedSlideType) {
      alert("Vui lòng chọn đầy đủ thông tin!")
      return
    }
    setIsGenerating(true)
    // Simulate API call
    setTimeout(() => {
      setIsGenerating(false)
      alert("Video đã được tạo thành công!")
    }, 3000)
  }

  const getCurrentChuongOptions = () => {
    return chuongOptions[selectedKhoi] || []
  }

  return (
    <main className="flex-grow">
      {/* Hero Section */}
      <section className="bg-gradient-to-r from-purple-500 to-purple-600 text-white py-20">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center">
            <h1 className="text-4xl md:text-6xl font-bold mb-6">
              Tạo video bài giảng <br />
              <span className="text-yellow-300">theo chương trình chuẩn</span>
            </h1>
            <p className="text-xl md:text-2xl mb-8 max-w-3xl mx-auto">
              Chọn khối lớp, chương học và kiểu slide - AI sẽ tự động tạo video bài giảng chuyên nghiệp
            </p>
            <div className="flex flex-col sm:flex-row gap-4 justify-center">
              <button className="bg-white text-purple-600 font-medium py-3 px-8 rounded-lg hover:bg-gray-100 transition-colors duration-200 shadow-md">
                Dùng thử miễn phí
              </button>
              <button className="border-2 border-white text-white font-medium py-3 px-8 rounded-lg hover:bg-white hover:text-purple-600 transition-colors duration-200">
                Xem demo
              </button>
            </div>
          </div>
        </div>
      </section>

      {/* Demo Section */}
      <section className="py-16 bg-gray-50">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-12">
            <h2 className="text-3xl font-bold text-gray-900 mb-4">Tạo video bài giảng ngay</h2>
            <p className="text-xl text-gray-600">Chỉ cần 3 bước đơn giản để có video bài giảng hoàn chỉnh</p>
          </div>

          <div className="bg-white rounded-xl shadow-lg p-8">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
              {/* Chọn khối */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  <GraduationCap className="w-4 h-4 inline mr-1" />
                  Chọn khối lớp
                </label>
                <div className="relative">
                  <select
                    value={selectedKhoi}
                    onChange={(e) => {
                      setSelectedKhoi(e.target.value)
                      setSelectedChuong("") // Reset chương khi đổi khối
                    }}
                    className="w-full p-3 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 focus:border-transparent appearance-none bg-white"
                  >
                    <option value="">Chọn khối lớp</option>
                    {khoiOptions.map((khoi) => (
                      <option key={khoi.value} value={khoi.value}>
                        {khoi.label}
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="absolute right-3 top-3.5 h-5 w-5 text-gray-400 pointer-events-none" />
                </div>
              </div>

              {/* Chọn chương */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  <BookOpen className="w-4 h-4 inline mr-1" />
                  Chọn chương học
                </label>
                <div className="relative">
                  <select
                    value={selectedChuong}
                    onChange={(e) => setSelectedChuong(e.target.value)}
                    disabled={!selectedKhoi}
                    className="w-full p-3 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 focus:border-transparent appearance-none bg-white disabled:bg-gray-100 disabled:cursor-not-allowed"
                  >
                    <option value="">Chọn chương</option>
                    {getCurrentChuongOptions().map((chuong) => (
                      <option key={chuong.value} value={chuong.value}>
                        {chuong.label}
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="absolute right-3 top-3.5 h-5 w-5 text-gray-400 pointer-events-none" />
                </div>
              </div>

              {/* Chọn kiểu slide */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  <Presentation className="w-4 h-4 inline mr-1" />
                  Chọn kiểu slide
                </label>
                <div className="relative">
                  <select
                    value={selectedSlideType}
                    onChange={(e) => setSelectedSlideType(e.target.value)}
                    className="w-full p-3 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 focus:border-transparent appearance-none bg-white"
                  >
                    <option value="">Chọn kiểu slide</option>
                    {slideTypes.map((type) => (
                      <option key={type.value} value={type.value}>
                        {type.label}
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="absolute right-3 top-3.5 h-5 w-5 text-gray-400 pointer-events-none" />
                </div>
              </div>
            </div>

            {/* Hiển thị thông tin đã chọn */}
            {(selectedKhoi || selectedChuong || selectedSlideType) && (
              <div className="mb-6 p-4 bg-purple-50 rounded-lg border border-purple-200">
                <h4 className="font-medium text-purple-900 mb-2">Thông tin đã chọn:</h4>
                <div className="text-sm text-purple-700 space-y-1">
                  {selectedKhoi && <p>• Khối: {khoiOptions.find((k) => k.value === selectedKhoi)?.label}</p>}
                  {selectedChuong && (
                    <p>• Chương: {getCurrentChuongOptions().find((c) => c.value === selectedChuong)?.label}</p>
                  )}
                  {selectedSlideType && (
                    <p>• Kiểu slide: {slideTypes.find((s) => s.value === selectedSlideType)?.label}</p>
                  )}
                </div>
              </div>
            )}

            {/* Nút tạo video */}
            <div className="text-center">
              <button
                onClick={handleGenerate}
                disabled={!selectedKhoi || !selectedChuong || !selectedSlideType || isGenerating}
                className="bg-gradient-to-r from-purple-500 to-purple-600 text-white font-medium py-4 px-8 rounded-lg hover:from-purple-600 hover:to-purple-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors duration-200 flex items-center justify-center mx-auto shadow-sm hover:shadow-md"
              >
                {isGenerating ? (
                  <>
                    <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-white mr-2"></div>
                    Đang tạo video...
                  </>
                ) : (
                  <>
                    <Wand2 className="w-5 h-5 mr-2" />
                    Tạo video bài giảng
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      </section>

      {/* Slide Types Section */}
      <section className="py-16">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-12">
            <h2 className="text-3xl font-bold text-gray-900 mb-4">Các kiểu slide có sẵn</h2>
            <p className="text-xl text-gray-600">Chọn kiểu slide phù hợp với nội dung bài học của bạn</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {slideTypes.map((type) => (
              <div
                key={type.value}
                className="bg-white p-6 rounded-lg shadow-sm hover:shadow-md transition-shadow duration-200 border border-gray-200"
              >
                <div className="w-12 h-12 bg-purple-100 rounded-lg flex items-center justify-center mb-4 text-purple-600">
                  {type.icon}
                </div>
                <h3 className="text-lg font-medium text-gray-900 mb-2">{type.label}</h3>
                <p className="text-gray-600 text-sm">{type.description}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* How it works */}
      <section className="py-16 bg-gray-50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-12">
            <h2 className="text-3xl font-bold text-gray-900 mb-4">Cách thức hoạt động</h2>
            <p className="text-xl text-gray-600">Quy trình đơn giản chỉ với 4 bước</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
            {steps.map((step, index) => (
              <div key={step.step} className="text-center">
                <div className="relative">
                  <div className="w-16 h-16 bg-gradient-to-r from-purple-500 to-purple-600 text-white rounded-full flex items-center justify-center mx-auto mb-4 text-xl font-bold">
                    {step.step}
                  </div>
                  {index < steps.length - 1 && (
                    <ArrowRight className="hidden md:block absolute top-8 -right-3 w-6 h-6 text-gray-300" />
                  )}
                </div>
                <h3 className="text-lg font-medium text-gray-900 mb-2">{step.title}</h3>
                <p className="text-gray-600 text-sm">{step.description}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Features Section */}
      <section className="py-16">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-12">
            <h2 className="text-3xl font-bold text-gray-900 mb-4">Tại sao chọn EduVision?</h2>
            <p className="text-xl text-gray-600">Công nghệ AI kết hợp chương trình giáo dục chuẩn</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8">
            {features.map((feature, index) => (
              <div
                key={index}
                className="bg-white p-6 rounded-lg shadow-sm hover:shadow-md transition-shadow duration-200"
              >
                <div className="w-16 h-16 bg-purple-100 rounded-lg flex items-center justify-center mb-4 text-purple-600">
                  {feature.icon}
                </div>
                <h3 className="text-xl font-medium text-gray-900 mb-2">{feature.title}</h3>
                <p className="text-gray-600">{feature.description}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Example Videos */}
      <section className="py-16 bg-gray-50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-12">
            <h2 className="text-3xl font-bold text-gray-900 mb-4">Video mẫu đã tạo</h2>
            <p className="text-xl text-gray-600">Xem các video bài giảng được tạo theo chương trình chuẩn</p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {exampleVideos.map((video) => (
              <div
                key={video.id}
                className="bg-white rounded-lg overflow-hidden shadow-sm hover:shadow-md transition-shadow duration-200 border border-gray-200"
              >
                <div className="relative">
                  <img
                    src={video.thumbnail || "/placeholder.svg"}
                    alt={video.title}
                    className="w-full h-48 object-cover"
                  />
                  <div className="absolute inset-0 bg-black bg-opacity-20 opacity-0 hover:opacity-100 transition-opacity duration-200 flex items-center justify-center">
                    <button className="bg-white rounded-full p-3 shadow-md hover:bg-gray-100 transition-colors duration-200">
                      <Play className="h-6 w-6 text-purple-600" />
                    </button>
                  </div>
                  <div className="absolute bottom-2 right-2 bg-black bg-opacity-70 text-white text-xs px-2 py-1 rounded">
                    {video.duration}
                  </div>
                </div>
                <div className="p-4">
                  <h3 className="font-medium text-gray-900 mb-2 line-clamp-2">{video.title}</h3>
                  <div className="space-y-1 mb-3">
                    <p className="text-xs text-gray-500">Khối: {video.khoi}</p>
                    <p className="text-xs text-gray-500">{video.chuong}</p>
                    <p className="text-xs text-purple-600 font-medium">{video.slideType}</p>
                  </div>
                  <div className="flex justify-between items-center text-xs text-gray-500">
                    <span>{video.views} lượt xem</span>
                    <div className="flex items-center">
                      <Star className="h-3 w-3 text-yellow-400 mr-1" />
                      <span>{video.rating}</span>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* CTA Section */}
      <section className="py-16 bg-gradient-to-r from-purple-500 to-purple-600 text-white">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
          <h2 className="text-3xl font-bold mb-4">Sẵn sàng tạo video bài giảng đầu tiên?</h2>
          <p className="text-xl mb-8">Tham gia cùng hàng nghìn giáo viên đã tin tưởng sử dụng EduVision</p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <button className="bg-white text-purple-600 font-medium py-3 px-8 rounded-lg hover:bg-gray-100 transition-colors duration-200 shadow-sm">
              Bắt đầu miễn phí
            </button>
            <button className="border-2 border-white text-white font-medium py-3 px-8 rounded-lg hover:bg-white hover:text-purple-600 transition-colors duration-200">
              Xem bảng giá
            </button>
          </div>

          <div className="mt-8 flex flex-wrap justify-center gap-6 text-sm">
            <div className="flex items-center">
              <CheckCircle className="w-4 h-4 mr-2" />
              Không cần thẻ tín dụng
            </div>
            <div className="flex items-center">
              <CheckCircle className="w-4 h-4 mr-2" />
              Dùng thử 7 ngày miễn phí
            </div>
            <div className="flex items-center">
              <CheckCircle className="w-4 h-4 mr-2" />
              Hỗ trợ 24/7
            </div>
          </div>
        </div>
      </section>
    </main>
  )
}

export default HomePage
